using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;

public class CarBehavior : MonoBehaviour
{   
    [Header("Input Properties")]
    public float steeringSmoothness = 1f; 
    private static float _steeringSmoothness;

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
    public int CurrentGear = 2;
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
    public int tcLevel = 2;
    public bool tcLBL = false;
    public float turningRadius = 1.5f;

    public float frontARB = 50000f;
    public float rearARB = 45000f;

    public float brakeForce = 2000f;
    public float brakeBalance = 0.6f;

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

    ///GUI 

    /// <summary>
    /// Alters the force point application distance as speeds increase, defaults to user settings at standstill. Acts as total vehicle stability in conjunction with ARB.
    /// </summary>
    public float Stability = 500f;

    [Header ("audio")]
    public AudioSource[] engineAudioSource = null;

    [Header("Camera Positions")]
    public Transform[] CameraPositions = null;


    [Header ("Down Force Zones")]

    //
    [SerializeField]
    public Transform[] downForceLocations;
    public float downForceMult = 20f;

    [Header ("Controller Settings")]
    public bool ControllerInput = true;
    private  float steerInputValue;
    private float accellerationInputValue;
    private float brakeInputValue; 
    private float gearUpInputValue;
    private float gearDownInputValue;
    private float clutchInputValue;
    private Vector3 spawnLocation;

    private char[] gearIndicator = new char[]{'R','N','1','2','3','4','5','6','7','8'};
    

    private void OnAccelerate(InputValue value){
        if(Gamepad.all.Count > 0){
            accellerationInputValue = value.Get<float>();
        } else {
            accellerationInputValue =0;
        }
    }

    private void OnBrake(InputValue value){
         if(Gamepad.all.Count > 0){
            brakeInputValue = value.Get<float>();
         }  else {
            accellerationInputValue = 0;
         }  
    }
    private void OnClutch(InputValue value){
        if(Gamepad.all.Count > 0){
            clutchInputValue = value.Get<float>(); 
        } else {
            clutchInputValue = 0;
        }
    }

    private void OnSteer(InputValue value){
        if(Gamepad.all.Count > 0){
            steerInputValue = value.Get<float>();
        } else {
            steerInputValue =0;
        }
    }
    private bool OnGearUp(){
        if(Gamepad.all.Count > 0){
            return Gamepad.current.buttonNorth.wasPressedThisFrame;
        } else { 
            return false; 
        }
    }
    private bool OnGearDown(){
         if(Gamepad.all.Count > 0){
        return Gamepad.current.buttonSouth.wasPressedThisFrame;
         } else {
            return false;
         }
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
    void Start(){   
        instanciateEmitters();
        DriveTranCheck();
        SuspensionProperties();
        audioCheck();
        calculateVehicleWheelBase();  
        spawnLocation = carTranform.position;

    }

    void instanciateEmitters(){
        if(tireTrail){
            frontRightTrail =  Instantiate(tireTrail, frontRight.transform.position - Vector3.up * (frontRight.radius/2 + _frontSuspensionHeight*2) , Quaternion.identity, frontRight.transform).GetComponent<TrailRenderer>();
            frontLeftTrail =   Instantiate(tireTrail, frontLeft.transform.position - Vector3.up *(frontLeft.radius/2 + _frontSuspensionHeight*2), Quaternion.identity,frontLeft.transform).GetComponent<TrailRenderer>();
            rearRightTrail =   Instantiate(tireTrail, rearRight.transform.position - Vector3.up *(rearRight.radius/2 + _rearSuspensionHeight*2), Quaternion.identity, rearRight.transform).GetComponent<TrailRenderer>();
            rearLeftTrail =    Instantiate(tireTrail, rearLeft.transform.position - Vector3.up * (rearLeft.radius/2  + _rearSuspensionHeight*2), Quaternion.identity, rearLeft.transform).GetComponent<TrailRenderer>();
        }
        if(tireSmoke){
            frontRightPart =  Instantiate(tireSmoke, frontRight.transform.position - Vector3.up * _frontWheelRadius, Quaternion.identity, frontRight.transform).GetComponent<ParticleSystem>();
            frontLeftPart =   Instantiate(tireSmoke, frontLeft.transform.position - Vector3.up * _frontWheelRadius, Quaternion.identity,frontLeft.transform).GetComponent<ParticleSystem>();
            rearRightPart =   Instantiate(tireSmoke, rearRight.transform.position - Vector3.up * _rearWheelRadius, Quaternion.identity, rearRight.transform).GetComponent<ParticleSystem>();
            rearLeftPart =    Instantiate(tireSmoke, rearLeft.transform.position - Vector3.up * _rearWheelRadius, Quaternion.identity, rearLeft.transform).GetComponent<ParticleSystem>();
        }  
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
        _steeringSmoothness = steeringSmoothness;
        _frontWheelRadius = (frontRight.radius + frontLeft.radius)/2f;
        _frontSuspensionHeight = (frontRight.suspensionDistance + frontLeft.suspensionDistance)/2f;
        _frontDamper = (frontRight.wheelDampingRate + frontLeft.wheelDampingRate)/2f;
        //frontSuspensionTravel = center - suspension height. 
        _rearWheelRadius = (rearRight.radius + rearLeft.radius)/2f;
        _rearSuspensionHeight = (rearRight.suspensionDistance + rearLeft.suspensionDistance)/2f;
        _rearDamper= (rearRight.wheelDampingRate + rearLeft.wheelDampingRate)/2f;
        ApplicationPointDist = (frontLeft.forceAppPointDistance+frontRight.forceAppPointDistance+rearLeft.forceAppPointDistance+rearRight.forceAppPointDistance)/4f;
    }
    private void audioCheck(){
        if(engineAudioSource != null){
            foreach(AudioSource engine in engineAudioSource){
                engine.Play();
            }
        }

    }
    private void calculateVehicleWheelBase(){
        wheelbase = (Vector3.Distance(frontLeft.transform.position, rearLeft.transform.position) + Vector3.Distance(frontRight.transform.position, rearRight.transform.position)) / 2f;
        rearTrack = Vector3.Distance(rearLeft.transform.position, rearRight.transform.position);
    }

    // Update is called once per frame
    void Update(){   
        AudioController();
        Transmition();
        CalculateSteerAngle();
        tireSkidEmission();

        getGroundHits();

        if(Input.GetKeyDown(KeyCode.R)){
            carTranform.position = spawnLocation;
            carTranform.rotation = new Quaternion(0f,0f,0f,0f);
        }
        if(Input.GetKeyDown(KeyCode.T)){
            if(tractionControl == true){
                tractionControl = false;
            } else {
                tractionControl = true;
            }
        }

    }

    private void AudioController(){
       if(engineAudioSource != null){
            foreach(AudioSource engine in engineAudioSource){
                float pitch = Mathf.Lerp(-0.4f, 1.7f, engineRPM / redLineRPM); 
                engine.pitch = pitch;
            }
        }
    }
    private void Transmition(){
        if(OnGearUp()  && CurrentGear < gearRatios.Length-1){
            CurrentGear += 1;
        } else if(Input.GetKeyDown(KeyCode.X)  && CurrentGear < gearRatios.Length-1){
            CurrentGear += 1;
        }
        if(OnGearDown()   && CurrentGear >= 0){
            CurrentGear -= 1;
        } else if(Input.GetKeyDown(KeyCode.Z)  && CurrentGear > 0){
            CurrentGear -= 1;
        }
    }
     private void CalculateSteerAngle(){
        
        if(steerInputValue > 0 || Input.GetAxis("Horizontal") > 0 ){
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
            steeringSmoothness = _steeringSmoothness * Mathf.Abs(wheelHits[0].sidewaysSlip + wheelHits[1].sidewaysSlip)/2;
        } else {
            steeringSmoothness = _steeringSmoothness;
        }
        
        targetLeftWAngle = Mathf.Lerp(targetLeftWAngle, desiredLeftWAngle, steeringSmoothness * Time.deltaTime);
        targetRightWAngle = Mathf.Lerp(targetRightWAngle, desiredRightWAngle, steeringSmoothness * Time.deltaTime);

        frontRight.steerAngle = targetRightWAngle / (1+(carRigidBody.velocity.magnitude/5f));
        frontLeft.steerAngle = targetLeftWAngle / (1+(carRigidBody.velocity.magnitude/5f));

        if(SteeringWheel != null){
            SteeringWheel.localRotation = Quaternion.Euler(23f, 0f ,-(frontRight.steerAngle+frontLeft.steerAngle)*2f );
        }
    }
     private void tireSkidEmission(){
        float skidlimit= 0.1f;
        float slipLimit = 0.2f;
        //i really should array the wheels up. but...
        if(Mathf.Abs(wheelHits[0].sidewaysSlip)  > skidlimit || Mathf.Abs(wheelHits[0].forwardSlip) >slipLimit){
            frontRightTrail.emitting = true;
            frontRightTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f, 1f *Mathf.Abs( wheelHits[0].sidewaysSlip + wheelHits[0].forwardSlip)));
        } else {
             frontRightTrail.emitting = false;
        }
        if(Mathf.Abs(wheelHits[1].sidewaysSlip)  > skidlimit || Mathf.Abs(wheelHits[1].forwardSlip) > slipLimit){
            frontLeftTrail.emitting = true;
            frontLeftTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f,1f *Mathf.Abs( wheelHits[1].sidewaysSlip + wheelHits[1].forwardSlip)));
        } else {
            frontLeftTrail.emitting = false;
        }
        if(Mathf.Abs(wheelHits[2].sidewaysSlip)  > skidlimit || Mathf.Abs(wheelHits[2].forwardSlip) > slipLimit){
            rearRightTrail.emitting = true;
            rearRightTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f, 2f *Mathf.Abs( wheelHits[2].sidewaysSlip + wheelHits[2].forwardSlip)));
        } else {
            rearRightTrail.emitting = false;
        }
        if(Mathf.Abs(wheelHits[3].sidewaysSlip)  > skidlimit|| Mathf.Abs(wheelHits[3].forwardSlip) > slipLimit){
            rearLeftTrail.emitting = true;
            rearLeftTrail.GetComponent<Renderer>().material.SetColor("_Color",new Color(0f, 0f, 0f, 2f *Mathf.Abs( wheelHits[3].sidewaysSlip + wheelHits[3].forwardSlip)));
        } else {
            rearLeftTrail.emitting = false;
        }
    }

    private void tireSmokeEmission(){
        float skidlimit= 0.15f;
        float slipLimit = 0.25f;
        if(Mathf.Abs(wheelHits[0].sidewaysSlip)  > skidlimit || Mathf.Abs(wheelHits[0].forwardSlip) > slipLimit){
             frontRightPart.startSpeed = carRigidBody.velocity.magnitude;
            frontRightPart.Play();
           
        }
        if(Mathf.Abs(wheelHits[1].sidewaysSlip)  > skidlimit || Mathf.Abs(wheelHits[1].forwardSlip) > slipLimit){
             frontLeftPart.startSpeed = carRigidBody.velocity.magnitude;
            frontLeftPart.Play();
            
        }
        if(Mathf.Abs(wheelHits[2].sidewaysSlip)  > skidlimit || Mathf.Abs(wheelHits[2].forwardSlip) > slipLimit){
            rearRightPart.startSpeed = carRigidBody.velocity.magnitude;
            rearRightPart.Play();
             
        }
        if(Mathf.Abs(wheelHits[3].sidewaysSlip)  > skidlimit|| Mathf.Abs(wheelHits[3].forwardSlip) > slipLimit){
            rearLeftPart.startSpeed = carRigidBody.velocity.magnitude;
            rearLeftPart.Play();
        } 
    }
    private void getGroundHits(){
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
        ApplyDownforce();
        ApplyfrontARB();
        ApplyrearARB();
        ApplyStabilization();
        ApplyTractionControl();
                tireSmokeEmission();
        
    }
    
    private void CalculateMotorTorque(){
        float AccelInput = Mathf.Clamp(accellerationInputValue + Input.GetAxis("Vertical"), 0, 1);
        clutch = 1- (clutchInputValue + Input.GetAxis("Fire3"));

        if((AccelInput == 0 && clutch < 0.2f) || (AccelInput == 0 && gearIndicator[CurrentGear] == 'N')){
            engineRPM -= 10f; //engine braking
        }
        if(rearWheelDrive){

            if(clutch < 0.2f){
                engineRPM = Mathf.Lerp(engineRPM, Mathf.Max(engineRPM, redLineRPM * AccelInput) + Random.Range(-50, 50), Time.deltaTime*4f);
                torqueOutput = 0;
            } else {
                wheelRPM = averageWheelRPM(rearRight,rearLeft) * gearRatios[CurrentGear] * (differentialRatio);
                engineRPM = Mathf.Lerp(engineRPM, Mathf.Max(idleRpm, wheelRPM), Time.deltaTime*2.5f);
                if(engineRPM < idleRpm){
                    engineRPM = idleRpm + Random.Range(50,-50);
                }
                /// Locked DIFF
                if(rearRight.rpm < rearLeft.rpm){
                    torqueOutput = ((((engineRPM* RPMToTorque.Evaluate(engineRPM))/5252f) * clutch * gearRatios[CurrentGear] ) + rearLeft.rpm*differentialLockRatio)/(1 - differentialLockRatio);
                }
                if(rearLeft.rpm < rearRight.rpm){
                    torqueOutput = ((((engineRPM* RPMToTorque.Evaluate(engineRPM))/5252f) * clutch * gearRatios[CurrentGear] ) + rearRight.rpm*differentialLockRatio)/(1 - differentialLockRatio);
                }

            }
        }
        if(allWheelDrive){
            engineRPM = averageWheelRPMAllwheelDrive(frontLeft, frontRight, rearLeft, rearRight);
        }
        if(frontWheelDrive){
            wheelRPM = averageWheelRPM(frontRight,frontLeft) * gearRatios[CurrentGear] * (differentialRatio);
        }

        if(engineRPM > redLineRPM-50f){
            engineRPM -= Random.Range(700,500);
            torqueOutput /= 1.3f;
        }
        rearLeft.motorTorque = torqueOutput * AccelInput;
        rearRight.motorTorque = torqueOutput  * AccelInput;

    }
    private float averageWheelRPM(WheelCollider left, WheelCollider right){
        return Mathf.Abs((left.rpm + right.rpm) / 2f);
    }
    private float averageWheelRPMAllwheelDrive(WheelCollider Fleft, WheelCollider Fright, WheelCollider Rleft, WheelCollider Rright){
        return ((Fleft.rpm+ Fright.rpm + Rleft.rpm + Rright.rpm) / 4f);
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
    private void calculateWheelExtremes(WheelCollider Wheel){
        WheelFrictionCurve frictionCurveR = Wheel.sidewaysFriction;
        float angularVelocity = 0f;
        float stiffnessIncrease = 0f;

        if(Wheel == frontLeft || Wheel == frontRight){

            if((steerInputValue > 0 && Wheel == frontLeft) || (Input.GetAxis("Horizontal") > 0 && Wheel == frontLeft)){ //Youre turning right and the outside wheels have their contact patch increased by the camber value. 
                angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _frontCamber;
                stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value
            } else if((steerInputValue < 0 && Wheel == frontRight) || (Input.GetAxis("Horizontal") < 0 && Wheel == frontRight)){
                angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _frontCamber;
                stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value
                
            }
        } else if (Wheel == rearRight ||Wheel == rearLeft){
            if((steerInputValue > 0 && Wheel == rearLeft) || (Input.GetAxis("Horizontal") > 0 && Wheel == rearLeft)){
                angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _rearCamber;
                stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value

                //// rrdrive calc
                //if(rearWheelDrive == true && Mathf.Abs(WheelSpinRatio(Wheel) )> 0.1f){
                //    steerInputValue =  - WheelSpinRatio(Wheel)* Mathf.Abs(carRigidBody.angularVelocity.z ); 
                //}
            }else if((steerInputValue < 0 && Wheel == rearRight) || (Input.GetAxis("Horizontal") < 0 && Wheel == rearRight)){
                angularVelocity = Mathf.Abs(carRigidBody.angularVelocity.y) * _rearCamber;
                stiffnessIncrease = Mathf.Clamp(angularVelocity, 0f, 1f); // Define the maximum stiffness increase value

                //wheel slip steering catch calculatuion
                //if(rearWheelDrive == true && Mathf.Abs(WheelSpinRatio(Wheel) )> 0.1f){
                //    steerInputValue =  + WheelSpinRatio(Wheel)* Mathf.Abs(carRigidBody.angularVelocity.z ); 
                //}
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
    private float WheelSpinRatio(WheelCollider wheelCollider){
        return  Mathf.Abs(wheelCollider.rpm+1f) / (Mathf.Abs(frontRight.rpm + frontLeft.rpm)+0.0000001f/2);
    }
    private void calculateToe(WheelCollider wheel){
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
    private void CalculateWheelPose(){
        SetPose(frontRight, frontRightWheel,  _frontCamber, _frontToe);
        SetPose(frontLeft, frontLeftWheel, _frontCamber, _frontToe);
        SetPose(rearRight, rearRightWheel, _rearCamber, _rearToe);
        SetPose(rearLeft, rearLeftWheel, _rearCamber, _rearToe);
    }
    private void SetPose(WheelCollider WC, Transform WT, float camberAngle = 0f, float toeAngle = 0f){
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
    private void ApplyBreakForce(){
        if(Input.GetAxis("Vertical") < 0){
        frontLeft.brakeTorque = Mathf.Abs(Input.GetAxis("Vertical")) * brakeForce * (1-brakeBalance);   // Axis Vertical is set to Posbutton = down Alt pos button = s; No negative buttons
        frontRight.brakeTorque  = Mathf.Abs(Input.GetAxis("Vertical")) * brakeForce * (1-brakeBalance); //
        rearRight.brakeTorque  = Mathf.Abs(Input.GetAxis("Vertical"))* brakeForce * (brakeBalance);
        rearLeft.brakeTorque  = Mathf.Abs(Input.GetAxis("Vertical")) * brakeForce * (brakeBalance);
        } else { // Gamepad input 
            frontLeft.brakeTorque = brakeInputValue * brakeForce * (1-brakeBalance);
            frontRight.brakeTorque  = brakeInputValue * brakeForce * (1-brakeBalance);
            rearRight.brakeTorque  = brakeInputValue * brakeForce * (brakeBalance);
            rearLeft.brakeTorque  = brakeInputValue * brakeForce * (brakeBalance);
        }
        
    }
    private void ApplyDownforce(){
        if(downForceLocations != null){
            foreach(Transform dfLocation in downForceLocations){
                RaycastHit hit;
                if(Physics.Raycast(dfLocation.position, transform.TransformDirection(Vector3.down), out hit, Mathf.Infinity)){
                    Debug.DrawRay(dfLocation.position, (-transform.up* (hit.distance*downForceMult) * Mathf.Abs(carRigidBody.velocity.z))/1000f, Color.yellow);
                        carRigidBody.AddForceAtPosition(-transform.up * (hit.distance*downForceMult)*  Mathf.Abs(carRigidBody.velocity.z), dfLocation.position);
                        //Debug.Log(-transform.up * 110f* carRigidBody.velocity.magnitude);
                        //Handles.Label(dfLocation.position, (hit.distance * 10f * Mathf.Abs(carRigidBody.velocity.z)).ToString());
                }
            }
        }
    }
    private void ApplyfrontARB(){
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
    private void ApplyrearARB(){
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
    private void ApplyStabilization(){
        float rbVel = carRigidBody.velocity.magnitude;
        frontRight.forceAppPointDistance = ApplicationPointDist + (rbVel /Stability);
        frontLeft.forceAppPointDistance =  ApplicationPointDist + (rbVel /Stability);
        rearRight.forceAppPointDistance =  ApplicationPointDist + (rbVel /Stability);
        rearLeft.forceAppPointDistance =   ApplicationPointDist + (rbVel /Stability);

        WheelFrictionCurve frictionCurve;
        if(!rearLeft.isGrounded){
            frictionCurve = rearRight.sidewaysFriction;
            frictionCurve.stiffness = 0f;
            rearRight.sidewaysFriction = frictionCurve;
        }
        if(!rearRight.isGrounded){
            frictionCurve = rearLeft.sidewaysFriction;
            frictionCurve.stiffness = 0f;
            rearRight.sidewaysFriction = frictionCurve;
        }
        if(!frontRight.isGrounded){
            frictionCurve = frontRight.sidewaysFriction;
            frictionCurve.stiffness = 0f;
            frontLeft.sidewaysFriction = frictionCurve;
        }
        if(!frontLeft.isGrounded){
            frictionCurve = frontLeft.sidewaysFriction;
            frictionCurve.stiffness = 0f;
            frontRight.sidewaysFriction = frictionCurve;
        }
    }

    private void ApplyTractionControl(){
                    //Active Traction Control. 
        tcLBL = false;
        if(tractionControl){
            if(rearRight.rpm > engineRPM){
                rearRight.brakeTorque = 5000f;
                tcLBL = true;
            }
            if(rearLeft.rpm > engineRPM){
                rearLeft.brakeTorque = 5000f;
                tcLBL = true;
            }
            if(Mathf.Abs(wheelHits[2].sidewaysSlip) > 0.14f|| Mathf.Abs(wheelHits[3].sidewaysSlip) > 0.14f ){
                torqueOutput = torqueOutput/tcLevel;
                rearLeft.brakeTorque = brakeForce/2f;
                rearRight.brakeTorque = brakeForce/2f;
                //engineRPM -= 5f * tcLevel;
                tcLBL = true;
            } 
        }
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
        //Chasis
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(frontLeft.transform.position, frontRight.transform.position);
        Gizmos.DrawLine(rearLeft.transform.position, rearRight.transform.position);
        Gizmos.DrawLine((rearLeft.transform.position + rearRight.transform.position)/2, (frontLeft.transform.position + frontRight.transform.position)/2);

        //ARB vectors
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(frontLeft.transform.position , transform.up * FrontantiRollVector.z);
        Gizmos.DrawRay(frontRight.transform.position , transform.up * FrontantiRollVector.z);          
        Gizmos.DrawRay(rearLeft.transform.position , transform.up * RantiRollVector.z);
        Gizmos.DrawRay(rearRight.transform.position ,  transform.up * RantiRollVector.z);

        //Driftgizmo,
        Gizmos.color = Color.red;
        Gizmos.DrawRay(carTranform.position, carRigidBody.angularVelocity.x * carRigidBody.velocity.magnitude* (transform.right ));

        Gizmos.color = Color.magenta;
        if(downForceLocations != null){
            foreach(Transform dfLoc in downForceLocations){
                Gizmos.DrawRay(dfLoc.position, -transform.up);
            }
        }
    }

    private void OnGUI()
    {   
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
       
        Rect labelRectGear= new Rect(Screen.width / 2, Screen.height - 220f, 190f, 90f);
        GUI.Label(labelRectGear, "Gear : " + gearIndicator[CurrentGear] );

       Rect labelRectRPM= new Rect(Screen.width / 2, Screen.height - 290f, 190f, 90f);
       GUI.Label(labelRectRPM, "RPM : " + engineRPM );

        Rect labelRect = new Rect(Screen.width / 2, Screen.height - 250f, 170f, 90f);
        GUI.Label(labelRect, "Speed KPH " + (carRigidBody.velocity.magnitude * 3.6f).ToString());

        Rect labelRectTC= new Rect(Screen.width / 3, Screen.height - 290f, 190f, 90f);
        if(tcLBL == true){
            GUI.Label(labelRectTC, "TC ON" );
        }

                  Rect ControlsLblR = new Rect(200f,200f,340f,140f);
        GUI.Label(ControlsLblR, "Reset: R");
          Rect ControlsLblC = new Rect(200f,220f,340f,140f);
        GUI.Label(ControlsLblC, "Change Camera: C");

        Rect ControlsLbl1 = new Rect(200f,250f,340f,140f);
        GUI.Label(ControlsLbl1, "Accellerate: W, or Arrow Up. RT gamepad");

         Rect ControlsLbl2 = new Rect(200f,270f,340f,140f);
        GUI.Label(ControlsLbl2, "Brake: S, or Arrow Down. LT gamepad");
        
        Rect ControlsLbl3 = new Rect(200f,290f,340f,140f);
        GUI.Label(ControlsLbl3, "Steer: Arrow R/L or A/D. Left stick gamepad");

                Rect ControlsLbl4 = new Rect(200f,310f,340f,140f);
        GUI.Label(ControlsLbl4, "Gear UP: X or Cross Gamepad");
        
                Rect ControlsLbl5 = new Rect(200f,330f,340f,140f);
        GUI.Label(ControlsLbl5, "Gear DOWN: Z Or Triangle Gamepad");

         Rect ControlsLbl6 = new Rect(200f,350f,340f,340f);
        GUI.Label(ControlsLbl6, "Clutch: Shift, or R1 gamepad");
    }
}

