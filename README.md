# KylesTrackDayController
A physically accurate car simulation that takes the unity wheel collider and actively manages it to create a very stable, and accurate vehicle simulation. With minimal tweaking you will be hitting the Apex or clutching up a sweet drift. 

Set up the heirarchy of your vehcile in a simular way you would by following unities wheel collider tutorial. 
![image](https://github.com/KyleRichards94/KylesTrackDayController/assets/122703065/1f9290fa-56da-48cf-bba2-9f539936499e)
heres my standard heirarchy. 

After applying a rigid body to your vehicles main transform fill in the required boxes. 
Wheel callipers and Axle transforms are optional - the simulation will work without them. 
![image](https://github.com/KyleRichards94/KylesTrackDayController/assets/122703065/48ba99a6-9708-4697-bde6-6f09cc06a890)

Some Toe and Camber are recommended to vastly improve corning capability at higher speeds. 
![image](https://github.com/KyleRichards94/KylesTrackDayController/assets/122703065/dd3a3026-8410-494e-bd9f-4057d812707f)

Your torque curve should physically mimic the torque curve produced by the car of your choice, this is shelby 500. After your cars redline you should push the curves value into negative values to simulate engine braking and transmission strain at the limit. 
**![image](https://github.com/KyleRichards94/KylesTrackDayController/assets/122703065/c88d2aa7-40e6-4ad1-8b1e-fa9ce7245b17)
**

Downforce zones are not optional. 
Apply three empties to the car in the hirearchy and position them along the cars center line. After supplying the script with the transforms a gizmo will show their position and force application 
![image](https://github.com/KyleRichards94/KylesTrackDayController/assets/122703065/4bd2aee6-9d7d-4e36-98ca-fb7761f7ef9f)


finally this is attempting to simulate real vehicle dynamics. as such use real numbers,very high spring forces, work best, 1400-2000kg rigid body weight 
![image](https://github.com/KyleRichards94/KylesTrackDayController/assets/122703065/37981317-f900-4e37-8f1a-4d5e9c1db92e)


this is a super quick and dirty commit, i will fixerup as weeks progress. 

