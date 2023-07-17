using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarBehavior : MonoBehaviour
{   
    [Header("Input Properties")]
    public float steeringSmoothness = 1f; 

    [Header("PowerTrain properties")]
    public float powerOutput = 500f;
    public float redLineRPM = 8000f;
    public float topSpeed;
    public float engineRPM;
    public float wheelRPM;
    public bool rearWheelDrive = true;
    public bool frontWheelDrive = false;
    public bool allWheelDrive = false;
    public float torqueOutput;
    public int CurrentGear = 0;

    private float idleRpm = 2500f;

    /// <summary>
    /// clutch defaults to 1 when user input is not detected, else clutch - input button. 
    /// <summary>
    private float clutch = 1f;
    /// <summary>
    /// Accurately conveys the torque from 0RPM to PeakRPM - HorsePower = RPM * Torque(Nm) / 5252
    /// </summary>
    public AnimationCurve RPMToTorque = new AnimationCurve(){
        keys = new Keyframe[]{
            new Keyframe(0f,320f), //Low RPM torque
            new Keyframe(1500f, 320f), //PowerGain
            new Keyframe(4500f, 400f), //PeakTorque
            new Keyframe(6500f, 350f), //PowerBand
            new Keyframe(9000f, -800f)   //DeadLineRPM,
        }
    };



    /// <summary>
    /// Holds the Gear position by index, and the gear ratio by value
    /// </summary>
    public float[] gearRatios = new float[]{3.14f, 2.05f, 1.43f, 1.1f, 0.86f, 0.68f}; 
    public float differentialRatio = 4.3f;
    public float differentialLockRatio = 0.4f;

    public float wheelbase;
    public float rearTrack;
    


    [Header("Car Transforms & Bodies")]
    public Transform carTranform;
    public Rigidbody carRigidBody;
    public Transform SteeringWheel;

    public Transform frontRightWheel;
    public Transform frontLeftWheel;
    public Transform rearRightWheel;
    public Transform rearLeftWheel;

    public Transform frontRightWheelCalliper;
    public Transform frontLeftWheelCalliper;
    public Transform rearRightWheelCalliper;
    public Transform rearLeftWheelCalliper;

    [Header("Wheel Colliders")]
    public WheelCollider frontRight;
    public WheelCollider frontLeft;
    public WheelCollider rearRight;
    public WheelCollider rearLeft;

    [Header("Handling Properties")]
    public bool tractionControl = true;
    public float turningRadius = 1.5f;


    public float frontARB = 5000f;
    public float rearARB = 4500f;

    private float FantiRollForceA;
    private float FantiRollForceB;  
    private Vector3 FrontantiRollVector;

    private float RantiRollForceA;
    private float RantiRollForceB;  
    private Vector3 RantiRollVector;

    [SerializeField] private float _frontWheelRadius;
    [SerializeField] private float _frontSuspensionHeight;
    [SerializeField] private float _frontSuspensionTravel;
    [SerializeField] private float _frontDamper;
    [SerializeField] private float _frontCamber;
    [SerializeField] private float _frontToe;
    [SerializeField] private float _rearWheelRadius;
    [SerializeField] private float _rearSuspensionHeight;
    [SerializeField] private float _rearSuspensionTravel;
    [SerializeField] private float _rearDamper;
    [SerializeField] private float _rearCamber;
    [SerializeField] private float _rearToe;

    private float ApplicationPointDist;
    private float desiredLeftWAngle; 
    private float desiredRightWAngle;
    public float targetLeftWAngle;
    public float targetRightWAngle;

    /// <summary>
    /// Alters the force point application distance as speeds increase, defaults to user settings at standstill. Acts as total vehicle stability in conjunction with ARB.
    /// </summary>
    public float Stability = 500f;

    [Header ("audio")]
    public AudioSource engineAudioSource;
    public AudioSource engineAudioSource2;

    [Header("Camera Positions")]
    public Transform[] CameraPositions = new Transform[3];

    [Header ("Down Force Zones")]
    public Transform FrontSplitter;
    public Transform hood; 
    public Transform rearWing;

    [Header ("Controller Settings")]
    public bool ControllerInput = true;
    private  float steerInputValue;
    private float accellerationInputValue;
    private float brakeInputValue; 
    private float gearUpInputValue;
    private float gearDownInputValue;
    private float clutchInputValue;
    

    private void OnAccelerate(InputValue value){
        accellerationInputValue = value.Get<float>();
    }

    private void OnBrake(InputValue value){
        brakeInputValue = value.Get<float>();    
    }
    private void OnClutch(InputValue value){
        clutchInputValue = value.Get<float>(); 
    }

    private void OnSteer(InputValue value){
        steerInputValue = value.Get<float>();
    }
    //private void OnGearup(InputValue value){   methods noto currently used
    //    gearUpInputValue = value.Get<float>();
    //}
    //private void OnGeardown(InputValue value){
    //    gearDownInputValue = value.Get<float>();
    //}

    [Header("Emmitters")] 
    public TrailRenderer tireTrail;
    private TrailRenderer frontRightTrail;
    private TrailRenderer frontLeftTrail;
    private TrailRenderer rearRightTrail;
    private TrailRenderer rearLeftTrail; 

    public ParticleSystem tireSmoke;
    private ParticleSystem frontRightPart;
    private ParticleSystem frontLeftPart;
    private ParticleSystem rearRightPart;
    private ParticleSystem rearLeftPart; 

    ///Wheel hits
    WheelHit[] wheelHits = new WheelHit[4];

    // Notes to me 
    // Use new wheelhit properties to increase grip at the limit. 
    // use new wheel hit properties to limit steering angle when the asymptote value is exceeded front wheels 
    // add tire smoke system to emmitparticles 
    // add tire squeel sound to each wheel, modulate volume and pitch with wheelrpm and wheelhit values.
    // 
    
    // Start is called before the first frame update
    void Start()
    {   
        instanciateEmitters();
        DriveTranCheck();
        SuspensionProperties();
        engineAudioSource.Play();
        engineAudioSource2.Play();

        wheelbase = (Vector3.Distance(frontLeft.transform.position, rearLeft.transform.position) + Vector3.Distance(frontRight.transform.position, rearRight.transform.position)) / 2f;
        rearTrack = Vector3.Distance(rearLeft.transform.position, rearRight.transform.position);
    }

    void instanciateEmitters(){
        if(tireTrail){
            frontRightTrail =  Instantiate(tireTrail, frontRight.transform.position - Vector3.up * (frontRight.radius*2) , Quaternion.identity, frontRight.transform).GetComponent<TrailRenderer>();
            frontLeftTrail =   Instantiate(tireTrail, frontLeft.transform.position - Vector3.up *(frontLeft.radius*2), Quaternion.identity,frontLeft.transform).GetComponent<TrailRenderer>();
            rearRightTrail =   Instantiate(tireTrail, rearRight.transform.position - Vector3.up *(rearRight.radius*2), Quaternion.identity, rearRight.transform).GetComponent<TrailRenderer>();
            rearLeftTrail =    Instantiate(tireTrail, rearLeft.transform.position - Vector3.up * (rearLeft.radius*2), Quaternion.identity, rearLeft.transform).GetComponent<TrailRenderer>();
        }
        if(tireSmoke){
            frontRightPart =  Instantiate(tireSmoke, frontRight.transform.position - Vector3.up * _frontWheelRadius, Quaternion.identity, frontRight.transform).GetComponent<ParticleSystem>();
            frontLeftPart =   Instantiate(tireSmoke, frontLeft.transform.position - Vector3.up * _frontWheelRadius, Quaternion.identity,frontLeft.transform).GetComponent<ParticleSystem>();
            rearRightPart =   Instantiate(tireSmoke, rearRight.transform.position - Vector3.up * _rearWheelRadius, Quaternion.identity, rearRight.transform).GetComponent<ParticleSystem>();
            rearLeftPart =    Instantiate(tireSmoke, rearLeft.transform.position - Vector3.up * _rearWheelRadius, Quaternion.identity, rearLeft.transform).GetComponent<ParticleSystem>();
        }  
    }

    // Update is called once per frame
    void Update()
    {   
        AudioController();
        Transmition();
        CalculateSteerAngle();
        TireSkidEmission();
        frontRight.GetGroundHit(out wheelHits[0]);
        frontLeft.GetGroundHit(out wheelHits[1]);
        rearRight.GetGroundHit(out wheelHits[2]);
        rearLeft.GetGroundHit(out wheelHits[3]);
    }

    void FixedUpdate() {
        CalculateMotorTorque();
        CalculateAxleForces();
        CalculateWheelPose(); 
        ApplyBreakForce();
        ApplyDownforce(FrontSplitter, 10f);
        ApplyDownforce(rearWing, 110f);
        ApplyDownforce(hood, 40f);
        ApplyfrontARB();
        ApplyrearARB();
        ApplyStabilization();


        clutch = 1- clutchInputValue;
    }

    private void calculateWheelExtremes(WheelCollider Wheel){
    WheelFrictionCurve frictionCurveR = Wheel.sidewaysFriction;
    float angularVelocity = 0f;
    float stiffnessIncrease = 0f;

    if(Wheel == frontLeft || Wheel == frontRight){

        if(steerInputValue > 0 && Wheel == frontLeft){ //Youre turning right and the outside wheels have their contact patch increased by the camber value. 
            angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _frontCamber;
            stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value
        } else if(steerInputValue < 0 && Wheel == frontRight){
            angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _frontCamber;
            stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value
        }
    } else if (Wheel == rearRight ||Wheel == rearLeft){
        if(steerInputValue > 0 && Wheel == rearLeft){
            angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _rearCamber;
            stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value
        }else if(steerInputValue < 0 && Wheel == rearRight){
            
            angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _rearCamber;
            stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value
        }
    }

    if(accellerationInputValue > 0){
        frictionCurveR.stiffness = Mathf.Clamp(2f - WheelSpinRatio(Wheel), 0.3f, 2f ) + stiffnessIncrease;
    } else {
        frictionCurveR.stiffness = 1 + stiffnessIncrease;
    }

    Wheel.sidewaysFriction = frictionCurveR ;
    Debug.DrawLine(Wheel.transform.position + (-transform.up * _rearWheelRadius), Wheel.transform.position + (transform.up * stiffnessIncrease), Color.red);
    }

    private void calculateWheelSpinSlip()
    {
        WheelFrictionCurve frictionCurveR = rearRight.sidewaysFriction;
        WheelFrictionCurve frictionCurveL = rearLeft.sidewaysFriction;
        frictionCurveR.stiffness = Mathf.Clamp(2f - WheelSpinRatio(rearRight), 0.6f, 1.5f );
        frictionCurveL.stiffness = Mathf.Clamp(2f - WheelSpinRatio(rearLeft), 0.6f, 1.5f );
        rearRight.sidewaysFriction = frictionCurveR;
        rearLeft.sidewaysFriction = frictionCurveL;
    }
    private float WheelSpinRatio(WheelCollider wheelCollider)
    {
        return  Mathf.Abs(wheelCollider.rpm+1f) / (Mathf.Abs(frontRight.rpm + frontLeft.rpm)+0.0000001f/2);
    }

    private void ApplyStabilization(){
        float rbVel = carRigidBody.velocity.magnitude;
        frontRight.forceAppPointDistance = ApplicationPointDist + (rbVel /Stability);
        frontLeft.forceAppPointDistance =  ApplicationPointDist + (rbVel /Stability);
        rearRight.forceAppPointDistance =  ApplicationPointDist + (rbVel /Stability);
        rearLeft.forceAppPointDistance =   ApplicationPointDist + (rbVel /Stability);
    }

    private void TireSkidEmission(){
        if(Mathf.Abs(wheelHits[0].sidewaysSlip)  > 0.25f || Mathf.Abs(wheelHits[0].forwardSlip) > 0.3f){
            frontRightTrail.emitting = true;
            frontRightTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f, 2f *Mathf.Abs( wheelHits[0].sidewaysSlip + wheelHits[0].forwardSlip)));
        } else {
             frontRightTrail.emitting = false;
        }
        if(Mathf.Abs(wheelHits[1].sidewaysSlip)  > 0.25f || Mathf.Abs(wheelHits[1].forwardSlip) > 0.3f){
            frontLeftTrail.emitting = true;
            frontLeftTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f,2f *Mathf.Abs( wheelHits[1].sidewaysSlip + wheelHits[1].forwardSlip)));
        } else {
            frontLeftTrail.emitting = false;
        }
        if(Mathf.Abs(wheelHits[2].sidewaysSlip)  > 0.25f || Mathf.Abs(wheelHits[2].forwardSlip) > 0.2f){
            rearRightTrail.emitting = true;
            rearRightTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f, 2f *Mathf.Abs( wheelHits[2].sidewaysSlip + wheelHits[2].forwardSlip)));
        } else {
            rearRightTrail.emitting = false;
        }
        if(Mathf.Abs(wheelHits[3].sidewaysSlip)  > 0.25f || Mathf.Abs(wheelHits[3].forwardSlip) > 0.2f){
            rearLeftTrail.emitting = true;
            rearLeftTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f, 2f *Mathf.Abs( wheelHits[3].sidewaysSlip + wheelHits[3].forwardSlip)));
        } else {
            rearLeftTrail.emitting = false;
        }
    }
    

    private void CalculateSteerAngle(){
        
        if(steerInputValue > 0 || Input.GetAxis("Horizontal") >0 ){
            desiredLeftWAngle = Mathf.Rad2Deg *  Mathf.Atan(wheelbase / (turningRadius ) + rearTrack / 2) * (steerInputValue + Input.GetAxis("Horizontal")) ;
            desiredRightWAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (turningRadius ) - rearTrack / 2) * (steerInputValue + Input.GetAxis("Horizontal")) ;
        }
        else if(steerInputValue < 0 || Input.GetAxis("Horizontal") < 0 ){
            desiredLeftWAngle = Mathf.Rad2Deg *  Mathf.Atan(wheelbase / (turningRadius ) - rearTrack / 2) * (steerInputValue + Input.GetAxis("Horizontal")) ;
            desiredRightWAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (turningRadius ) + rearTrack / 2) * (steerInputValue + Input.GetAxis("Horizontal")) ;
        } else {
            desiredLeftWAngle = 0;
            desiredRightWAngle = 0;
        }

        // Apply lerp to smoothly transition between the current and desired values
        if(wheelHits[0].sidewaysSlip > 0.2f && wheelHits[1].sidewaysSlip > 0.2f){
            steeringSmoothness = 20f * Mathf.Abs(wheelHits[0].sidewaysSlip + wheelHits[1].sidewaysSlip)/2;
        } else {
            steeringSmoothness = 20f;
        }
        
        targetLeftWAngle = Mathf.Lerp(targetLeftWAngle, desiredLeftWAngle, steeringSmoothness * Time.deltaTime);
        targetRightWAngle = Mathf.Lerp(targetRightWAngle, desiredRightWAngle, steeringSmoothness * Time.deltaTime);

        frontRight.steerAngle = targetRightWAngle/(1+(carRigidBody.velocity.magnitude/10f));
        frontLeft.steerAngle = targetLeftWAngle/(1+(carRigidBody.velocity.magnitude/10f));

        if(SteeringWheel != null){
            SteeringWheel.localRotation = Quaternion.Euler(23f, 0f ,-(targetLeftWAngle+targetRightWAngle) );
        }
    }
    
    //BROKEN
private void ApplyfrontARB()
{
    WheelHit hitA, hitB;

    bool groundedA = frontLeft.GetGroundHit(out hitA);
    bool groundedB = frontRight.GetGroundHit(out hitB);

    if (groundedA && groundedB)
    {
        float travelA = (-frontLeft.transform.InverseTransformPoint(hitA.point).z - frontLeft.radius) / frontLeft.suspensionDistance;
        float travelB = (-frontRight.transform.InverseTransformPoint(hitB.point).z - frontRight.radius) / frontRight.suspensionDistance;

        FantiRollForceA = travelA * frontARB;
        FantiRollForceB = travelB * frontARB;

        FrontantiRollVector.z = (FantiRollForceA - FantiRollForceB);

        carRigidBody.AddForceAtPosition(frontLeft.transform.up * FrontantiRollVector.z, frontLeft.transform.position);
        carRigidBody.AddForceAtPosition(frontRight.transform.up * -FrontantiRollVector.z, frontRight.transform.position);

        // Draw Gizmos for the forces
    }
}

private void ApplyrearARB()
{
    WheelHit hitA, hitB;

    bool groundedA = rearLeft.GetGroundHit(out hitA);
    bool groundedB = rearRight.GetGroundHit(out hitB);

    if (groundedA && groundedB)
    {
        float travelA = (-rearLeft.transform.InverseTransformPoint(hitA.point).z - rearLeft.radius) / rearLeft.suspensionDistance;
        float travelB = (-rearRight.transform.InverseTransformPoint(hitB.point).z - rearRight.radius) / rearRight.suspensionDistance;

        RantiRollForceA = travelA * rearARB;
        RantiRollForceB = travelB * rearARB;

        RantiRollVector.z = (RantiRollForceA - RantiRollForceB);

        carRigidBody.AddForceAtPosition(rearLeft.transform.up * RantiRollVector.z, rearLeft.transform.position);
        carRigidBody.AddForceAtPosition(rearRight.transform.up * -RantiRollVector.z, rearRight.transform.position);

        // Draw Gizmos for the forces
    }
}



    private void ApplyDownforce(Transform downforceLocation, float value){
        RaycastHit hit;
        if(Physics.Raycast(downforceLocation.position, transform.TransformDirection(Vector3.down), out hit, Mathf.Infinity)){
             Debug.DrawRay(downforceLocation.position, (-transform.up* value*  Mathf.Abs(carRigidBody.velocity.z))/1000f, Color.yellow);
                carRigidBody.AddForceAtPosition(-transform.up * value*  Mathf.Abs(carRigidBody.velocity.z), downforceLocation.position);
                //Debug.Log(-transform.up * 110f* carRigidBody.velocity.magnitude);
        }
    }

    private void CalculateMotorTorque(){
        float AccelInput = Mathf.Clamp(accellerationInputValue + Input.GetAxis("Vertical"), 0, 1);

        if(engineRPM > redLineRPM-50f){
            engineRPM -= Random.Range(600,500);
        }
        if(accellerationInputValue == 0){
            engineRPM -= 20f; //engine braking
        }

        if(rearWheelDrive){

                    if(clutch < 0.2f){
                        engineRPM = Mathf.Lerp(engineRPM, Mathf.Max(engineRPM, redLineRPM * AccelInput) + Random.Range(-50, 50), Time.deltaTime*4f);
                        torqueOutput = 0;
                    } else {
                        float targetRPM = Mathf.Max(engineRPM, engineRPM);
                        wheelRPM = averageWheelRPM(rearRight,rearLeft) * gearRatios[CurrentGear] * (differentialRatio);
                        engineRPM = Mathf.Lerp(engineRPM, Mathf.Max(idleRpm, wheelRPM), Time.deltaTime*3f);
                        if(engineRPM < idleRpm){
                            engineRPM = idleRpm + Random.Range(50,-50);
                        }
                        /// Locked DIFF
                        if(rearRight.rpm < rearLeft.rpm){
                            torqueOutput = ((((engineRPM* RPMToTorque.Evaluate(engineRPM))/5252f) * clutch * gearRatios[CurrentGear] * 1.8f) + rearLeft.rpm*differentialLockRatio)/1.2f;
                        }
                        if(rearLeft.rpm < rearRight.rpm){
                            torqueOutput = ((((engineRPM* RPMToTorque.Evaluate(engineRPM))/5252f) * clutch * gearRatios[CurrentGear] * 1.8f) + rearRight.rpm*differentialLockRatio)/1.2f;
                        }
                        //Active Traction Control. 
                        if(rearRight.rpm > engineRPM){
                            rearRight.brakeTorque = 1600f;
                        }

                        if(rearLeft.rpm > engineRPM){
                            rearLeft.brakeTorque = 1600f;
                        }

                        if(tractionControl){
                            if(Mathf.Abs(wheelHits[2].sidewaysSlip) > 0.2f || Mathf.Abs(wheelHits[3].sidewaysSlip) > 0.2f){
                                torqueOutput = 0;
                                rearLeft.brakeTorque = 1600f;
                                rearRight.brakeTorque = 1600f;
                                engineRPM -= 50f;
                            }
                        }
                    }
            }
            if(allWheelDrive){
                engineRPM = averageWheelRPMAllwheelDrive(frontLeft, frontRight, rearLeft, rearRight);
            }
            if(frontWheelDrive){
                wheelRPM = averageWheelRPM(frontRight,frontLeft) * gearRatios[CurrentGear] * (differentialRatio);
            }

            rearLeft.motorTorque = torqueOutput * AccelInput;
            rearRight.motorTorque = torqueOutput  * AccelInput;

    }
    private void ApplyBreakForce(){
            if(Input.GetAxis("Vertical") < 0){
            frontLeft.brakeTorque = Mathf.Abs(Input.GetAxis("Vertical")) * 1600f;   // Axis Vertical is set to Posbutton = down Alt pos button = s; No negative buttons
            frontRight.brakeTorque  = Mathf.Abs(Input.GetAxis("Vertical")) * 1600f; //
            rearRight.brakeTorque  = Mathf.Abs(Input.GetAxis("Vertical"))* 1000f;
            rearLeft.brakeTorque  = Mathf.Abs(Input.GetAxis("Vertical")) * 1000f;
            } else { // Gamepad input 
                frontLeft.brakeTorque = brakeInputValue * 1600f;
                frontRight.brakeTorque  = brakeInputValue * 1600f;
                rearRight.brakeTorque  = brakeInputValue * 1000f;
                rearLeft.brakeTorque  = brakeInputValue * 1000f;
            }
        
    }
    private void Transmition(){
        if(Gamepad.current.buttonNorth.wasPressedThisFrame  && CurrentGear < gearRatios.Length-1){
            CurrentGear += 1;
        } else if(Input.GetKeyDown(KeyCode.X)  && CurrentGear < gearRatios.Length-1){
            CurrentGear += 1;
        }
        if((Gamepad.current.buttonSouth.wasPressedThisFrame || Input.GetKeyDown(KeyCode.Z))   && CurrentGear >= 0){
            CurrentGear -= 1;
        } else if(Input.GetKeyDown(KeyCode.Z)  && CurrentGear >= 0){
            CurrentGear -= 1;
        }
    }
    private void AudioController(){
        float pitch = Mathf.Lerp(-0.1f, 1.2f, engineRPM / redLineRPM); 
        float pitch2 = Mathf.Lerp(0.3f,1.4f, engineRPM / redLineRPM); 
        engineAudioSource.pitch = pitch;
        engineAudioSource2.pitch = pitch2;
    }

private void CalculateWheelPose()
{
    SetPose(frontRight, frontRightWheel,  _frontCamber, _frontToe);
    SetPose(frontLeft, frontLeftWheel, _frontCamber, _frontToe);
    SetPose(rearRight, rearRightWheel, _rearCamber, _rearToe);
    SetPose(rearLeft, rearLeftWheel, _rearCamber, _rearToe);
}

private void SetPose(WheelCollider WC, Transform WT, float camberAngle = 0f, float toeAngle = 0f)
{
    //may need to transition to a mesh 
    Quaternion q;
    Vector3 pos;
    WC.GetWorldPose(out pos, out q);

    // Create a new quaternion with X and Y from q and Z from camberAngle
    Quaternion Toe = Quaternion.Euler(0, toeAngle , 0);
    Quaternion Camber = Quaternion.Euler(1,1,camberAngle);



    WT.transform.position = pos;
    WT.transform.rotation = q;
    //WT.transform.rotation = Quaternion.Euler(q.eulerAngles.x * Axle.eulerAngles.x,  q.eulerAngles.y* Axle.eulerAngles.y, q.eulerAngles.z* Axle.eulerAngles.z);
    WT.transform.rotation = q;
    //WT.transform.localRotation *= Camber;

}

private void CalculateAxleForces(){

    calculateWheelExtremes(rearRight);
    calculateWheelExtremes(rearLeft);
    calculateWheelExtremes(frontRight);
    calculateWheelExtremes(frontLeft);

    calculateToe(rearRight);
    calculateToe(rearLeft);
    calculateToe(frontRight);
    calculateToe(frontLeft);


}
private void calculateToe(WheelCollider wheel)
{
    Vector3 forceDirection = new Vector3();
    float forceMagnitude = 0f;

    if (wheel == rearRight){
        forceDirection = -transform.right;
        forceMagnitude = _rearToe * Mathf.Abs(wheelHits[2].sidewaysSlip) * Mathf.Abs(carRigidBody.velocity.z);
    }
    else if (wheel == rearLeft){
        forceDirection = transform.right;
        forceMagnitude = _rearToe * Mathf.Abs(wheelHits[3].sidewaysSlip) * Mathf.Abs(carRigidBody.velocity.z);
    }
    else if (wheel == frontRight){
        forceDirection = -transform.right;
        forceMagnitude = _frontToe * Mathf.Abs(wheelHits[0].sidewaysSlip) * Mathf.Abs(carRigidBody.velocity.z);
    }
    else if (wheel == frontLeft){
        forceDirection = transform.right;
        forceMagnitude = _frontToe * Mathf.Abs(wheelHits[1].sidewaysSlip) * Mathf.Abs(carRigidBody.velocity.z);
    }
    

    Vector3 forceVector = forceDirection * forceMagnitude;

    carRigidBody.AddForceAtPosition(forceVector, wheel.transform.position);
    // Draw the force line
    Debug.DrawLine(wheel.transform.position, wheel.transform.position + forceVector, Color.red);
}

private float averageWheelRPM(WheelCollider left, WheelCollider right){
    return Mathf.Abs((left.rpm + right.rpm) / 2f);
}
private float averageWheelRPMAllwheelDrive(WheelCollider Fleft, WheelCollider Fright, WheelCollider Rleft, WheelCollider Rright){
    return ((Fleft.rpm+ Fright.rpm + Rleft.rpm + Rright.rpm) / 4f);
}

private void DriveTranCheck(){
    int countTrue = 0; 
    if(frontWheelDrive){ countTrue ++;}
    if(rearWheelDrive){ countTrue ++;}
    if(allWheelDrive){ countTrue ++; }
    if(countTrue > 1 || countTrue < 1){
        Debug.Log("Ensure only 1 powetran type is selected: setting default");
        rearWheelDrive = true;
        frontWheelDrive = false;
        allWheelDrive = false;
    }
}

private void SuspensionProperties(){
    _frontWheelRadius = (frontRight.radius + frontLeft.radius)/2f;
    _frontSuspensionHeight = (frontRight.suspensionDistance + frontLeft.suspensionDistance)/2f;
    _frontDamper = (frontRight.wheelDampingRate + frontLeft.wheelDampingRate)/2f;
    //frontSuspensionTravel = center - suspension height. 
    _rearWheelRadius = (rearRight.radius + rearLeft.radius)/2f;
    _rearSuspensionHeight = (rearRight.suspensionDistance + rearLeft.suspensionDistance)/2f;
    _rearDamper= (rearRight.wheelDampingRate + rearLeft.wheelDampingRate)/2f;
    ApplicationPointDist = (frontLeft.forceAppPointDistance+frontRight.forceAppPointDistance+rearLeft.forceAppPointDistance+rearRight.forceAppPointDistance)/4f;
}
   //void SuspensionPropertiesUpdate(){
   //    //Front
   //    frontLeft.wheelRadius = _frontWheelRadius;
   //    frontRight.wheelRadius = _frontWheelRadius;
   //    frontLeft.suspensionDistance = _frontSuspensionHeight;
   //    frontRight.suspensionDistance = _frontSuspensionHeight;
   //    frontLeft.damperStiffness = _frontDamper;
   //    frontRight.damperStiffness = _frontDamper;
   //    //rear
   //    rearLeft.wheelRadius = _frontWheelRadius;
   //    rearRight.wheelRadius = _frontWheelRadius;
   //    rearLeft.suspensionDistance = _frontSuspensionHeight;
   //    rearRight.suspensionDistance = _frontSuspensionHeight;
   //    rearLeft.damperStiffness = _frontDamper;
   //    rearRight.damperStiffness = _frontDamper;
   //}

    private void OnDrawGizmos()
    {
        // Draw Gizmos lines between the wheels
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(frontLeft.transform.position, frontRight.transform.position);
        Gizmos.DrawLine(rearLeft.transform.position, rearRight.transform.position);
        Gizmos.DrawLine((rearLeft.transform.position + rearRight.transform.position)/2, (frontLeft.transform.position + frontRight.transform.position)/2);

                         Gizmos.color = Color.blue;
            Gizmos.DrawRay(frontLeft.transform.position, FrontantiRollVector);
            Gizmos.DrawRay(frontRight.transform.position, -FrontantiRollVector);
                                     
            Gizmos.DrawRay(rearLeft.transform.position, RantiRollVector);
            Gizmos.DrawRay(rearRight.transform.position, - RantiRollVector);

            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(rearWing.position, -transform.up);
            Gizmos.DrawRay(hood.position, -transform.up);
            Gizmos.DrawRay(FrontSplitter.position, -transform.up);
    }

    private void OnGUI()
    {   
              // Set the position and size of the GUI label
        Rect labelRect = new Rect(10f, 10f, 100f, 20f);

        // Display the number as text using GUI.Label
        GUI.Label(labelRect, "Speed KPH " + carRigidBody.velocity.magnitude *3.6f);

        Rect groudSlip1 = new Rect(Screen.width / 2 - 70f, 10f, 140f, 90f);
        GUI.Label(groudSlip1, "frSS: " + wheelHits[0].sidewaysSlip);
        Rect groudSlip2 = new Rect(Screen.width / 2 + 50f, 10f, 140f, 90f);
        GUI.Label(groudSlip2, "flSS: " + wheelHits[1].sidewaysSlip);
        Rect groudSlip3 = new Rect(Screen.width / 2 - 70f, 30f, 140f, 90f);
        GUI.Label(groudSlip3, "rrSS: " + wheelHits[2].sidewaysSlip);
        Rect groudSlip4 = new Rect(Screen.width / 2 + 50f, 30f, 140f, 90f);
        GUI.Label(groudSlip4, "rlSS: " + wheelHits[3].sidewaysSlip);

        Rect forwardSlip1 = new Rect(Screen.width / 2 - 70f, 50f, 140f, 90f);
        GUI.Label(forwardSlip1, "frFS: " + wheelHits[0].forwardSlip);
        Rect forwardSlip2 = new Rect(Screen.width / 2 + 50f, 50f, 140f, 90f);
        GUI.Label(forwardSlip2, "flFS: " + wheelHits[1].forwardSlip);
        Rect forwardSlip3 = new Rect(Screen.width / 2 - 70f, 70f, 140f, 90f);
        GUI.Label(forwardSlip3, "rrFS: " + wheelHits[2].forwardSlip);
        Rect forwardSlip4 = new Rect(Screen.width / 2 + 50f, 70f, 140f, 90f);
        GUI.Label(forwardSlip4, "rlFS: " + wheelHits[3].forwardSlip);
        Rect labelRect2 = new Rect(40f, 40f, 140f, 60f);
        GUI.Label(labelRect2, "ForwardSlip FY" + rearRight.forwardFriction.stiffness);
        Rect labelRect3 = new Rect(40f, 90f, 140f, 90f);
        GUI.Label(labelRect3, "Clutch: " + clutch);
                Rect labelRect4 = new Rect(40f, 130f, 190f, 90f);
        GUI.Label(labelRect4, "FR compression: " );
    }
}
