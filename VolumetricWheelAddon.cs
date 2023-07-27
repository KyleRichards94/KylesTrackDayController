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


    private WheelCollider _wheelCollider;
    public GameObject carController;
    private float originalRadius;
    private float originalSuspension;
    public Vector3 orgCenter;
    public Vector3 currentPosition;
    public Vector3 originalPosition;
    private Rigidbody rb; 
    private CarBehavior cb;

    [Header("Suspension Calculations")]
    private float springForce;
    private Vector3 suspensionForce;
    private float CurSuspensionLength;
    private float lastLength;
    private float springVelocity;
    private float fx; 
    private float fy; 
    public Vector3 wheelVelocityLS;
    private float springLength;
    private float minLength;
    private float maxLength;

    private float SuspensionStiffness;
    private float damperStiffness;

    private float SuspensionTravel;

    private float epsilon = 0.003f;

    void Awake()
    {
        _wheelCollider = GetComponent<WheelCollider>();
        //carController = GetComponentInParent<___CarControllerMonoBehaviourName___>();
        originalRadius = _wheelCollider.radius;
        originalSuspension = _wheelCollider.suspensionDistance;
        
        originalPosition = _wheelCollider.transform.position;
        rb = carController.GetComponent<Rigidbody>();

        cb = carController.GetComponent<CarBehavior>();
        
        SuspensionStiffness = _wheelCollider.suspensionSpring.spring;
        damperStiffness = _wheelCollider.suspensionSpring.damper;
        SuspensionTravel = _wheelCollider.suspensionDistance/2;
        minLength = _wheelCollider.suspensionDistance - _wheelCollider.suspensionDistance/2;
        maxLength =  _wheelCollider.suspensionDistance + SuspensionTravel/2;
    }
    
    void Update()
    {
        currentPosition = _wheelCollider.transform.position;

        if (!wheelModel)
            return;


        float radiusOffset = 0f;
        float suspensionOffset = 0f;
        int countHits = 0;

            for (int i = 0; i <= raysNumber; i++)
            {
               
                for(int j = -RayArraySize; j <= RayArraySize; j++){
                    
                    Vector3 rayDirection = Quaternion.AngleAxis(_wheelCollider.steerAngle, transform.up) * Quaternion.AngleAxis(i * (raysMaxAngle / raysNumber) + ((180f - raysMaxAngle) / 2), transform.right) * transform.forward;
                    Debug.DrawRay(wheelModel.position + (wheelModel.right * wheelWidth * j/10f), rayDirection * (originalRadius + tireWallHeight), Color.green);
                    if (Physics.Raycast(wheelModel.position + (wheelModel.right * wheelWidth * j/10f), rayDirection, out RaycastHit hit, _wheelCollider.radius + tireWallHeight))
                    {
                        countHits++;
                        Debug.Log(countHits);
                        if (!hit.transform.IsChildOf(carController.transform) && !hit.collider.isTrigger)
                        {
                            //float angle = Vector3.Angle(hit.normal, Vector3.up);
                            //float targetPosition = _wheelCollider.suspensionDistance * Mathf.Clamp01(angle / 90f);
                            Debug.DrawLine(wheelModel.position + (wheelModel.right * wheelWidth * j/10f), hit.point, Color.red);

                            //If theres a hit Completely override the wheel collider suspension system 
                            // and replace it with a propietary spring at the position of the hit. 
                            //Nullify the wheelcollider spring. 
                            // Update the WheelCollider's suspension spring with the new force
      
                            // Apply the force to the rb at the contact point

                            JointSpring suspensionSpring = _wheelCollider.suspensionSpring;
                            suspensionSpring.spring = 0f;
                            suspensionSpring.damper = 0f;
                            // Set the updated suspension spring back to the WheelCollider
                            _wheelCollider.suspensionSpring = suspensionSpring;

                            //Force at the tyre rubber, keeps the wheel in place (Sortof)
                            Vector3 forceAtContact = (rb.mass*2f/(countHits+Mathf.Abs(rb.velocity.magnitude))) * hit.normal * ((_wheelCollider.radius + tireWallHeight) - hit.distance) ;
                             rb.AddForceAtPosition(forceAtContact, hit.point, ForceMode.Impulse);
                            //Spring calaculation
                            lastLength = springLength;
                            springLength = hit.distance - originalRadius; //the length of the spring
                            springLength = Mathf.Clamp(springLength, minLength, maxLength);
                            springVelocity = (lastLength - springLength) /Time.fixedDeltaTime;
                            CurSuspensionLength =_wheelCollider.suspensionDistance - (springLength); // length of ray - wheel dist = final length of suspension
                            float Spring_F = SuspensionStiffness * CurSuspensionLength; 
                            float damperForce = damperStiffness * springVelocity;
                            springForce = SuspensionStiffness * (_wheelCollider.suspensionDistance  - springLength);
                            suspensionForce = (Mathf.Clamp((springForce + damperForce),0, rb.mass) *  transform.up)/(countHits + rb.velocity.magnitude + 10f);
                            _wheelCollider.suspensionDistance = lastLength + epsilon;
                            // Calculates the Basic friction force at the contact points in place of the wheel collider PaceJka equation
                            float normalizedSuspensionMag = suspensionForce.normalized.magnitude;
                            float normalizedSuspensionForce  = normalizedSuspensionMag/rb.mass;
                            float fx = cb.torqueOutput * Input.GetAxis("Vertical");
                            Vector3 wheelVelocityLS = transform.InverseTransformDirection(rb.GetPointVelocity(hit.point)); 
                            float fy = ((wheelVelocityLS.x)  * (_wheelCollider.suspensionSpring.spring -  normalizedSuspensionForce))/(countHits);
                            rb.AddForceAtPosition(suspensionForce + (fy * transform.right) + (fx * -transform.forward) , transform.position, ForceMode.Force);

                            break;

                        }
                         
                    } else {
                        _wheelCollider.suspensionDistance= Mathf.LerpUnclamped(_wheelCollider.suspensionDistance, originalSuspension, Time.deltaTime * 0.006f);

                         JointSpring suspensionSpring = _wheelCollider.suspensionSpring;
                        suspensionSpring.spring = Mathf.LerpUnclamped(_wheelCollider.suspensionSpring.spring , SuspensionStiffness, Time.deltaTime * 0.001f);
                        suspensionSpring.damper = Mathf.LerpUnclamped(_wheelCollider.suspensionSpring.damper , damperStiffness, Time.deltaTime * 0.001f);
                        _wheelCollider.suspensionSpring = suspensionSpring;
                    }
                }
            }
        }
}
