using UnityEngine;

[RequireComponent(typeof(WheelCollider))]
public class VolumetricWheelAddon : MonoBehaviour
{
    [Space(15)]
    [Range(2, 360)]
    public int raysNumber = 36;
    [Range(0f, 360f)]
    public float raysMaxAngle = 180f;
    [Range(0f, 1f)]
    public float wheelWidth = .25f;
    [Range(1,5)]
    public int RayArraySize = 2;
    public float tireWallHeight = 0.02f;
    [Space(15)]
    public Transform wheelModel;
    [Range(0f,360f)]
    public float allowableAngle = 15f;
    [Range(0.1f, 1.3f)]
    public float tirePressure = 0.75f;

    
    private WheelCollider _wheelCollider;
    
 
    public GameObject carController;
    private float originalRadius;
    private float originalSuspension;
    public Vector3 originalPosition;
    private Rigidbody rb; 
    private CarBehavior cb;

    [Header("Suspension Calculations")]
    private float springForce;
    private Vector3 suspensionForce;
    private float CurSuspensionLength;
    private float lastLength;
    private float springVelocity;
    private float springLength;
    private float minLength;
    private float maxLength;

    private float fy;
    private float fx;
    private float SuspensionStiffness;
    private float damperStiffness;

    private float SuspensionTravel;

    private float epsilon = 0.003f;

    public int countHits;

    public TotalVolumetricCounter total;
    public bool contact = false;

    private Vector3 hitpoint;

    private float hitdist= 0f;

    private float originalSuspensionTarget;
    private float currentSuspensionTarget;
    private float lastGroundedSuspensionLength;

    public bool isDriveWheel = false;

    void Awake()
    {
        _wheelCollider = GetComponent<WheelCollider>();
        //carController = GetComponentInParent<___CarControllerMonoBehaviourName___>();
        originalRadius = _wheelCollider.radius;
        originalSuspension = _wheelCollider.suspensionDistance;
        
        originalPosition = _wheelCollider.transform.localPosition;

        rb = carController.GetComponent<Rigidbody>();
        cb = carController.GetComponent<CarBehavior>();
        
        SuspensionStiffness = _wheelCollider.suspensionSpring.spring;
        damperStiffness = _wheelCollider.suspensionSpring.damper;
        SuspensionTravel = _wheelCollider.suspensionDistance/2;
        minLength = _wheelCollider.suspensionDistance - _wheelCollider.suspensionDistance/2;
        maxLength =  _wheelCollider.suspensionDistance + SuspensionTravel/2;
    }
    void Start(){
        originalSuspensionTarget = _wheelCollider.suspensionSpring.targetPosition;
        // Initialize the current suspension target position to the original position
        currentSuspensionTarget = originalSuspensionTarget;
    }
    
    void FixedUpdate()
    {
        
        setWheelPose();

        if (!wheelModel)
            return;

        countHits = 0;

        WheelFrictionCurve forwardFriction = _wheelCollider.forwardFriction;
        WheelFrictionCurve sidewaysFriction = _wheelCollider.sidewaysFriction;
        
        lastGroundedSuspensionLength = _wheelCollider.suspensionDistance;
        if(!_wheelCollider.isGrounded){
                currentSuspensionTarget = 0f;

                JointSpring suspensionSpring = new JointSpring{
                    targetPosition = currentSuspensionTarget,
                    spring = _wheelCollider.suspensionSpring.spring,
                    damper = _wheelCollider.suspensionSpring.damper,
                };
        }


            for (int i = 0; i <= raysNumber; i++)
            {
                    if(!_wheelCollider.isGrounded){
                            currentSuspensionTarget = 0f;
                    }

               
                for(int j = -RayArraySize; j <= RayArraySize; j++){
                    Vector3 rayDirection = Quaternion.AngleAxis(_wheelCollider.steerAngle, transform.up) * Quaternion.AngleAxis(i * (raysMaxAngle / raysNumber) + ((180f - raysMaxAngle) / 2), transform.right) * transform.forward;
                    if(j == 0 ){
                        j++;
                    }
                   // Debug.Log(Vector3.Angle(rayDirection, -Vector3.up));
                     if (Vector3.Angle(rayDirection, -Vector3.up) > allowableAngle ){
                        Debug.DrawRay(wheelModel.position + (wheelModel.right * wheelWidth * j/12f), rayDirection * (originalRadius + tireWallHeight), Color.green);
                        if (Physics.Raycast(wheelModel.position + (wheelModel.right * wheelWidth * j/10f), rayDirection, out RaycastHit hit, _wheelCollider.radius + tireWallHeight)){
                            countHits++;
                            contact = true;
                            //Debug.Log(countHits);
                            if (!hit.transform.IsChildOf(carController.transform) && !hit.collider.isTrigger)
                            {   
                                
                                //Grab the wheel friction curve from the wheel 
                                // evaluate the forward and sideways friction from it. 
                                // apply the forces to fy. 
                                currentSuspensionTarget = 0f;
                                JointSpring suspensionSpring = new JointSpring
                                {
                                    targetPosition = currentSuspensionTarget,
                                    spring = _wheelCollider.suspensionSpring.spring,
                                    damper = _wheelCollider.suspensionSpring.damper
                                };
                                
                                _wheelCollider.suspensionSpring = suspensionSpring;
                                
                                float slip = CalculateFriction(rb.angularVelocity.magnitude, forwardFriction);
                                Debug.Log(slip);

                                Debug.DrawLine(wheelModel.position + (wheelModel.right * wheelWidth * j/10f), hit.point, Color.red);

                                float speed = rb.velocity.magnitude;
                                
                                lastLength = springLength;
                                springLength = _wheelCollider.suspensionDistance - lastLength; //the length of the spring
                                

                                Debug.Log("hit distance" + hit.distance);
                                Debug.Log("Spring length " + springLength);

                                hitdist = hit.distance  + epsilon;

                                springLength = Mathf.Clamp(springLength, minLength, maxLength);
                                springVelocity = Mathf.Abs(lastLength - springLength) / Time.fixedDeltaTime*0.1f;
                                Debug.Log("Last Len" + lastLength);
                                CurSuspensionLength =_wheelCollider.suspensionDistance - (springLength); // length of ray - wheel dist = final length of suspension
                                float Spring_F = SuspensionStiffness * CurSuspensionLength; 
                                float damperForce = damperStiffness * springVelocity;

                                float normalizedSuspensionMag = suspensionForce.normalized.magnitude;
                                float normalizedSuspensionForce  = normalizedSuspensionMag/rb.mass;

                                if(isDriveWheel){
                                    fx = carController.GetComponent<CarBehavior>().ActiveTorqueOut;
                                } else {
                                    fx = 0;
                                }

                                Vector3 wheelVelocityLS = transform.InverseTransformDirection(rb.GetPointVelocity(hit.point)); 

                                 fy = ((wheelVelocityLS.x)  * ( _wheelCollider.suspensionSpring.spring -  normalizedSuspensionForce))/(countHits+total.totalCount);

                                springForce = SuspensionStiffness * (_wheelCollider.suspensionDistance  - springLength) * (tirePressure/Mathf.Abs(hit.distance));
                                suspensionForce = ((springForce + damperForce) *(transform.up/(rb.velocity.magnitude+epsilon) +  hit.normal))/((countHits+total.totalCount));
                               
                                //_wheelCollider.suspensionDistance = Mathf.Lerp(_wheelCollider.suspensionDistance, _wheelCollider.suspensionDistance/2f , Time.deltaTime * rb.velocity.magnitude);
                                
//
                                //Vector3 forceAtContact = (((rb.mass/2f)/(countHits*total.totalCount+1))) * (hit.normal) * ((_wheelCollider.suspensionDistance -  hit.distance)/countHits);
                                _wheelCollider.ResetSprungMasses();
                                    
                                rb.AddForceAtPosition(  suspensionForce + ((slip)*-transform.right) + (fx * transform.forward)  , transform.position, ForceMode.Force);

                                // The popping occurs because of the TargetPosition variable. It needs to be set to 0 when enter a collision and eased back when exiting collision.

                                //_wheelCollider.transform.position = new Vector3(_wheelCollider.transform.position.x, _wheelCollider.transform.position.y + (hit.distance/55f),_wheelCollider.transform.position.z);
            
                            

                                break;

                            }
                        }
                         
                    } else {
                        
                        currentSuspensionTarget = Mathf.Lerp(currentSuspensionTarget, originalSuspensionTarget, Time.deltaTime * 1f);
                        if(!_wheelCollider.isGrounded){
                            currentSuspensionTarget = 0f;
                             //_wheelCollider.suspensionDistance =  _wheelCollider.suspensionDistance/2f;
                        }
                        JointSpring suspensionSpring = new JointSpring{
                            targetPosition = currentSuspensionTarget,
                            spring = _wheelCollider.suspensionSpring.spring,
                            damper = _wheelCollider.suspensionSpring.damper
                        };

                        // Apply the new suspensionSpring to the WheelCollider
                        _wheelCollider.suspensionSpring = suspensionSpring;
                        contact = false;
                        _wheelCollider.suspensionDistance= Mathf.Lerp(_wheelCollider.suspensionDistance, originalSuspension, Time.deltaTime * rb.velocity.magnitude/10f);

                        // JointSpring suspensionSpring = _wheelCollider.suspensionSpring;
                        //suspensionSpring.spring = Mathf.LerpUnclamped(_wheelCollider.suspensionSpring.spring , SuspensionStiffness, Time.deltaTime * 1f);
                        //suspensionSpring.damper = Mathf.LerpUnclamped(_wheelCollider.suspensionSpring.damper , damperStiffness, Time.deltaTime * 2f);
                       // _wheelCollider.suspensionSpring = suspensionSpring;

                        _wheelCollider.transform.localPosition = originalPosition;
                        Quaternion q;
                        Vector3 pos;
                        //Vector3 offSet = WC.GetComponent("VolumetricWheelAddon").currentPosition;
                        //Vector3 posWithOffset = WC.GetComponent("VolumetricWheelAddon").currentPosition
                        _wheelCollider.GetWorldPose(out pos, out q);
                        //Quaternion Toe = Quaternion.Euler(0f, toeAngle , 0f);
                        //Quaternion Camber = Quaternion.Euler(0f,0f,-camberAngle*10f);
                        //WT.transform.localRotation = Camber * Toe * q;
                        wheelModel.transform.rotation = q;
                        wheelModel.transform.position = new Vector3(pos.x,pos.y,pos.z);
                        //+(hitdist - originalRadius)
                        
                        //newWheelCollider.transform.position =  _wheelCollider.transform.position;
                    }
                }
            }
        }

    private void setWheelPose(){
            _wheelCollider.transform.localPosition = originalPosition;
            Quaternion q;
            Vector3 pos;
            //Vector3 offSet = WC.GetComponent("VolumetricWheelAddon").currentPosition;
            //Vector3 posWithOffset = WC.GetComponent("VolumetricWheelAddon").currentPosition
            _wheelCollider.GetWorldPose(out pos, out q);
            //Quaternion Toe = Quaternion.Euler(0f, toeAngle , 0f);
            //Quaternion Camber = Quaternion.Euler(0f,0f,-camberAngle*10f);
            //WT.transform.localRotation = Camber * Toe * q;
            wheelModel.transform.rotation = q;
            wheelModel.transform.position = new Vector3(pos.x,pos.y,pos.z);

    }

    private void OnGUI()
    {
        float startX = Screen.width * 2 / 3;
        float startY = Screen.height / 3;

        GUI.Label(new Rect(startX, startY, 100, 20), "Speed");
        startY += 20;
        GUI.Label(new Rect(startX, startY, 100, 20), "Spring Length");
        startY += 20;
        GUI.Label(new Rect(startX, startY, 100, 20), "Hit Distance");
        startY += 20;
        GUI.Label(new Rect(startX, startY, 100, 20), "Last Length");
        startY += 20;
        GUI.Label(new Rect(startX, startY, 100, 20), "Cur Suspension Length");
        startY += 20;
        GUI.Label(new Rect(startX, startY, 100, 20), "Spring Force");
        startY += 20;
        GUI.Label(new Rect(startX, startY, 100, 20), "Suspension Force");
      
        GUI.Label(new Rect(startX + 100, startY, 100, 20), rb.velocity.magnitude.ToString());
        startY += 20;
        GUI.Label(new Rect(startX + 100, startY, 100, 20), springLength.ToString());
        startY += 20;
        GUI.Label(new Rect(startX + 100, startY, 100, 20), hitdist.ToString());
        startY += 20;
        GUI.Label(new Rect(startX + 100, startY, 100, 20), lastLength.ToString());
        startY += 20;
        GUI.Label(new Rect(startX + 100, startY, 100, 20), CurSuspensionLength.ToString());
        startY += 20;
        GUI.Label(new Rect(startX + 100, startY, 100, 20), springForce.ToString());
        startY += 20;
        GUI.Label(new Rect(startX + 100, startY, 100, 20), suspensionForce.ToString());
    }

    float CalculateFriction(float slipSpeed, WheelFrictionCurve frictionCurve)
    {
        float extremumSlip = frictionCurve.extremumSlip;
        float extremumValue = frictionCurve.extremumValue;
        float asymptoteSlip = frictionCurve.asymptoteSlip;
        float asymptoteValue = frictionCurve.asymptoteValue;
        float friction = 0f;

        if (slipSpeed < extremumSlip)
        {
            friction = extremumValue * slipSpeed / extremumSlip;
        }
        else
        {
            friction = asymptoteValue * (1f - (slipSpeed - extremumSlip) / (asymptoteSlip - extremumSlip));
        }

        return friction * frictionCurve.stiffness;
    }

}
