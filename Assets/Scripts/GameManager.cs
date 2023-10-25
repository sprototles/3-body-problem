using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

public class GameManager : MonoBehaviour
{

    #region Variables

    [Header("Managers")]
    public CanvasManager canvasManager;

    [Header("enum")]
    public EnumProcessType enumProcessType;
    public EnumTimer enumTimer;

    public enum EnumProcessType { MainThread, IJob, ComputeShader_1D, ComputeShader_2D };
    public enum EnumTimer { Update, FixedUpdate, OneSecond };

    /// <summary>
    /// set to True when start simulation
    /// </summary>
    [Header("Data")]
    public bool simulationInProgress = false;

    /// <summary>
    /// set to True when objects were instantiated
    /// </summary>
    public bool gameobjectInstantiated = false;

    /// <summary>
    /// since "delta time" is variable, current value is stored here
    /// </summary>
    public float currentDeltaTime;

    /// <summary>
    /// number of spawned objects
    /// </summary>
    public int sizeOfArray;

    // constant
    public readonly float G = 0.00000000006675f;

    /// <summary>
    /// play with this value to speed up / slow down calculated force values
    /// </summary>
    [SerializeField]
    private float forceMultiplier;

    /// <summary>
    /// for visual effect in editor
    /// </summary>
    [SerializeField]
    private float drawLine = 1f;

    /// <summary>
    /// store last N calculation time
    /// </summary>
    [SerializeField]
    private List<float> m_ListAverageTime = new List<float>();

    [SerializeField]
    private Material mat;

    /******************************/
    /*******  MAIN THREAD  ********/
    /******************************/

    /// <summary>
    /// array of moon gameobjects
    /// </summary>
    private GameObject[] moonGameobjects;

    /// <summary>
    /// array of moon rigidbodies
    /// </summary>
    private Rigidbody[] moonRigidbodies;

    /// <summary>
    /// array of moon transform.position
    /// </summary>
    private Vector3[] moonPositions;

    /// <summary>
    /// array of calculated forces applied on moons
    /// </summary>
    private Vector3[] moonForces;

    /// <summary>
    /// array of moons.rigidbody mass
    /// </summary>
    private float[] moonMasses;


    /******************************/
    /**********  JOBS  ************/
    /******************************/

    // moon objects
    NativeArray<Vector3> m_MoonPositions;
    NativeArray<float> m_MoonMasses;
    NativeArray<Vector3> m_MoonForces;


    /******************************/
    /******* COMPUTE SHADERS ******/
    /******************************/

    [Header("Compute shader")]
    public ComputeShader computeShader1D;
    public ComputeShader computeShader2D;
    public ComputeBuffer moonPositionBuffer;    // A buffer for moon positions
    public ComputeBuffer moonMassBuffer;        // A buffer for moon masses
    public ComputeBuffer resultBuffer;          // A buffer for the results

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        sizeOfArray = 1000;
        canvasManager.UpdateInputFieldText(sizeOfArray.ToString());

        forceMultiplier = 1000000f;

        enumTimer = EnumTimer.OneSecond;
        enumProcessType = EnumProcessType.MainThread;


        InvokeRepeating("OneSecondUpdate", 1.0f, 0.99f);
    }

    // ########################
    // ##### Update
    // ########################

    // Update is called once per frame
    void Update()
    {
        if (enumTimer != EnumTimer.Update)
            return;

        RunSimulation();
    }

    private void FixedUpdate()
    {
        if (enumTimer != EnumTimer.FixedUpdate)
            return;

        RunSimulation();
    }

    void OneSecondUpdate()
    {

        if (enumTimer != EnumTimer.OneSecond)
            return;

        RunSimulation();
    }

    // ########################
    // ##### Init
    // ########################

    #region Spawn, Update and Destroy moons

    /// <summary>
    /// update size of array
    /// </summary>
    /// <param name="value"></param>
    public void SetMaxArray(int value)
    {
        sizeOfArray = value;
    }


    private void InstantiateMoons()
    {
        Debug.Log("InstantiateMoons", this);

        // set array size
        moonPositions = new Vector3[sizeOfArray];
        moonMasses = new float[sizeOfArray];
        moonForces = new Vector3[sizeOfArray];
        moonGameobjects = new GameObject[sizeOfArray];
        moonRigidbodies = new Rigidbody[sizeOfArray];

        for (int i = 0; i < sizeOfArray; i++)
        {
            float randomScale = UnityEngine.Random.Range(2, 11);    // random scale size
            Vector3 spawnPosition = new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100));

            moonGameobjects[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moonGameobjects[i].transform.position = spawnPosition;
            moonGameobjects[i].transform.SetParent(transform);
            moonGameobjects[i].transform.localScale = new Vector3(randomScale, randomScale, randomScale);
            moonGameobjects[i].GetComponent<Renderer>().material = mat;

            moonPositions[i] = moonGameobjects[i].transform.position;
            moonMasses[i] = UnityEngine.Random.Range(1, 10000); // random mass value

            moonRigidbodies[i] = moonGameobjects[i].AddComponent<Rigidbody>();
            moonRigidbodies[i].mass = moonMasses[i];
            moonRigidbodies[i].useGravity = false;

            moonForces[i] = Vector3.zero;
        }

        // job system
        m_MoonPositions = new NativeArray<Vector3>(moonPositions, Allocator.Persistent);
        m_MoonMasses = new NativeArray<float>(moonMasses, Allocator.Persistent);
        m_MoonForces = new NativeArray<Vector3>(moonForces, Allocator.Persistent);

    }

    public void UpdateMoonVectors()
    {
        for (int i = 0; i < sizeOfArray; i++)
        {
            // for main threat
            moonPositions[i] = moonGameobjects[i].transform.position;

            // for job system
            m_MoonPositions.Dispose();
            m_MoonPositions = new NativeArray<Vector3>(moonPositions, Allocator.Persistent);
        }
    }

    private void DestroyMoons()
    {
        Debug.Log("DestroyMoons", this);

        foreach (GameObject go in moonGameobjects)
        {
            Destroy(go);
        }

        // zero out arrays
        moonPositions = new Vector3[0];
        moonMasses = new float[0];
        moonForces = new Vector3[0];
        moonGameobjects = new GameObject[0];
        moonRigidbodies = new Rigidbody[0];
    }

    private void OnDestroy()
    {
        DestroyMoons();
    }

    #endregion

    // ########################
    // ##### Simulation
    // ########################

    private void RunSimulation()
    {
        Debug.Log("RunSimulation", this);

        // update myDeltaTime value
        GetCurrentDeltaTime();

        if (!simulationInProgress)
            return;

        // Debug.Log("DoCalculation",this);

        float startTime = Time.realtimeSinceStartup;

        // ######################
        // START COMPUTATION HERE
        // ######################

        UpdateMoonVectors();

        switch (enumProcessType)
        {
            case EnumProcessType.MainThread:
                MainThreatCalculations();
                break;

            case EnumProcessType.IJob:
                IJobCalculations();
                // IJobForCalculations();
                break;

            case EnumProcessType.ComputeShader_1D:
                ComputeShaderCalculations1D();
                break;

            case EnumProcessType.ComputeShader_2D:
                ComputeShaderCalculations2D();
                break;
        }

        // ####################
        // END COMPUTATION HERE
        // ####################

        float duration = (Time.realtimeSinceStartup - startTime) * 1000f;

        m_ListAverageTime.Add(duration);
        if (m_ListAverageTime.Count > 50)  //Remove the oldest when we have more than 50
        {
            m_ListAverageTime.RemoveAt(0);
        }

        float averageDuration = 0f;
        foreach (float f in m_ListAverageTime)  //Calculate the total of all floats
        {
            averageDuration += f;
        }
        float averateTime = averageDuration / (float)m_ListAverageTime.Count;

        // set text
        string text = duration.ToString("F2") + " ms / " + averateTime.ToString("F2") + " ms";
        canvasManager.UpdateSimulationTimerText(text);

    }

    // ########################
    // ##### MAIN THREAD
    // ########################

    #region Main threat

    private void MainThreatCalculations()
    {
        for (int i = 0; i < sizeOfArray; i++)
        {
            Vector3 force = CalculateForceVector(moonPositions[i], moonMasses[i]) * currentDeltaTime * forceMultiplier;
            moonRigidbodies[i].AddForce(force);

            // for visuals only
            Debug.DrawLine(moonPositions[i], moonPositions[i] + force * drawLine, Color.white, 0.25f);
        }
    }

    private Vector3 CalculateForceVector(Vector3 planetVector, float planetMass)
    {
        Vector3 forcesOnObject = Vector3.zero;

        // 
        for (int i = 0; i < sizeOfArray; i++)
        {
            // calculate vector difference between selected moon and iterated moon
            Vector3 vector3Distance = planetVector - moonPositions[i];

            if (vector3Distance == Vector3.zero)
                continue;

            // convert distance to float
            float distance = Mathf.Sqrt(vector3Distance.sqrMagnitude);

            // calculate force applied on selected moon
            float sizeOfForce = G * ((moonMasses[i] * planetMass) / distance);

            // calculate direction of force
            Vector3 diretionOfForce = (moonPositions[i] - planetVector).normalized;

            // multiply direction and size and sums it up
            forcesOnObject += (diretionOfForce * sizeOfForce);
        }
        return forcesOnObject;
    }

    #endregion

    // ########################
    // ##### JOB SYSTEM
    // ########################

    #region IJob

    private void IJobCalculations()
    {
        for (int i = 0; i < sizeOfArray; i++)
        {
            Vector3 force = Calculate_IJob(moonPositions[i], moonMasses[i]) * currentDeltaTime * forceMultiplier;
            moonRigidbodies[i].AddForce(force);

            // visuals only
            Debug.DrawLine(moonPositions[i], moonPositions[i] + force * drawLine, Color.white, 0.25f);

        }
    }

    public Vector3 Calculate_IJob(Vector3 planetVector, float planetMass)
    {
        Vector3 forcesOnObject = Vector3.zero;

        // copy arrays to NativeArray for job system
        Gravity_IJob m_GravityIJob = new Gravity_IJob()
        {
            j_PlanetVector = planetVector,
            j_PlanetMass = planetMass,
            j_MoonVectors = m_MoonPositions,
            j_MoonMasses = m_MoonMasses,
            j_MoonForces = m_MoonForces
        };

        // execute job
        m_GravityIJob.Schedule().Complete();
        m_GravityIJob.j_MoonForces.CopyTo(m_MoonForces);

        // calculate final force from returned values
        foreach (Vector3 moonForce in m_MoonForces)
        {
            forcesOnObject += moonForce;
        }

        return forcesOnObject;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct Gravity_IJob : IJob
    {
        public Vector3 j_PlanetVector;
        public float j_PlanetMass;

        public NativeArray<Vector3> j_MoonVectors;
        public NativeArray<float> j_MoonMasses;
        public NativeArray<Vector3> j_MoonForces;

        public void Execute()
        {
            for (var i = 0; i < j_MoonVectors.Length; i++)
            {
                // calculate vector difference between selected moon and iterated moon
                var vector3Distance = j_PlanetVector - j_MoonVectors[i];

                if (vector3Distance == Vector3.zero)
                    continue;

                // convert distance to float
                var distance = Mathf.Sqrt(vector3Distance.sqrMagnitude);

                // calculate force applied on selected moon
                var sizeOfForce = 0.00000000006675f * ((j_MoonMasses[i] * j_PlanetMass) / distance);

                // calculate direction of force
                var diretionOfForce = (j_MoonVectors[i] - j_PlanetVector).normalized;

                // multiply direction and size, summary is done after jobs done
                j_MoonForces[i] = (diretionOfForce * sizeOfForce);
            }
        }
    }

    #endregion

    #region IJobFor

    private void IJobForCalculations()
    {
        for (int i = 0; i < sizeOfArray; i++)
        {
            Vector3 force = Calculate_IJobFor(moonPositions[i], moonMasses[i]);
            moonRigidbodies[i].AddForce(force * currentDeltaTime * forceMultiplier);
        }
    }

    public Vector3 Calculate_IJobFor(Vector3 planetVector, float planetMass)
    {
        Vector3 forcesOnObject = Vector3.zero;

        Gravity_IJobFor m_GravityIJobFor = new Gravity_IJobFor()
        {
            j_PlanetVector = planetVector,
            j_PlanetMass = planetMass,
            j_MoonVectors = m_MoonPositions,
            j_MoonMasses = m_MoonMasses,
            j_MoonForces = m_MoonForces
        };

        JobHandle sheduleJobDependency = new JobHandle();
        JobHandle m_JobHandleJobFor = m_GravityIJobFor.Schedule(sizeOfArray, sheduleJobDependency);
        m_JobHandleJobFor.Complete();
        m_GravityIJobFor.j_MoonForces.CopyTo(m_MoonForces);

        foreach (Vector3 moonForce in m_MoonForces)
        {
            forcesOnObject += moonForce;
        }

        Debug.DrawLine(planetVector, planetVector + forcesOnObject * 10000, Color.white, 0.25f);

        return forcesOnObject;

    }

    [BurstCompile]
    struct Gravity_IJobFor : IJobFor
    {
        public Vector3 j_PlanetVector;
        public float j_PlanetMass;

        public NativeArray<Vector3> j_MoonVectors;
        public NativeArray<float> j_MoonMasses;
        public NativeArray<Vector3> j_MoonForces;

        public void Execute(int i)
        {
            // calculate vector difference between selected moon and iterated moon
            var v3Distance = j_PlanetVector - j_MoonVectors[i];

            if (v3Distance == Vector3.zero)
                return;

            // convert distance to float
            var distance = Mathf.Sqrt(v3Distance.sqrMagnitude);

            // calculate force applied on selected moon
            var sizeOfForce = 0.00000000006675f * ((j_MoonMasses[i] * j_PlanetMass) / distance);

            var diretionOfForce = (j_MoonVectors[i] - j_PlanetVector).normalized;

            j_MoonForces[i] = (diretionOfForce * sizeOfForce);
        }
    }

    #endregion


    // ########################
    // ##### COMPUTE SHADERS
    // ########################

    #region Compute shader

    private void ComputeShaderCalculations1D()
    {
        int kernelHandle = computeShader1D.FindKernel("CSMain");

        // Create the moon vector buffer
        moonPositionBuffer = new ComputeBuffer(sizeOfArray, sizeof(float) * 3);

        // Create the moon mass buffer
        moonMassBuffer = new ComputeBuffer(sizeOfArray, sizeof(float));

        // Create a result buffer
        resultBuffer = new ComputeBuffer(sizeOfArray, sizeof(float) * 3);

        // Set the result buffer in the compute shader
        computeShader1D.SetBuffer(kernelHandle, "ResultBuffer", resultBuffer);
        computeShader1D.SetBuffer(kernelHandle, "moonPositions", moonPositionBuffer);
        computeShader1D.SetBuffer(kernelHandle, "moonMasses", moonMassBuffer);

        moonPositionBuffer.SetData(moonPositions);
        moonMassBuffer.SetData(moonMasses);

        // Set the actual moon count
        computeShader1D.SetInt("gridSize", sizeOfArray);

        // Dispatch the compute shader with the correct number of thread groups = [numthreads(32, 1, 1)]
        // int threadX = 32, threadY = 1, threadZ = 1;
        computeShader1D.Dispatch(kernelHandle, sizeOfArray / 64, 1, 1);

        // Read back the result from the GPU into an array
        Vector3[] resultData = new Vector3[sizeOfArray];
        resultBuffer.GetData(resultData);


        // 1D SOLUTION

        // Process the result as needed in your Start() method
        for (int i = 0; i < sizeOfArray; i++)
        {
            Vector3 force = resultData[i] * currentDeltaTime * forceMultiplier;
            moonRigidbodies[i].AddForce(force);

            Debug.DrawLine(moonPositions[i], moonPositions[i] + force * drawLine, Color.white, 0.25f);
        }
    }


    private void ComputeShaderCalculations2D()
    {
        int kernelHandle = computeShader2D.FindKernel("CSMain");

        // Create the moon vector buffer
        moonPositionBuffer = new ComputeBuffer(sizeOfArray, sizeof(float) * 3);

        // Create the moon mass buffer
        moonMassBuffer = new ComputeBuffer(sizeOfArray, sizeof(float));

        // Create a result buffer
        resultBuffer = new ComputeBuffer(sizeOfArray, sizeof(float) * 3);

        // Set the result buffer in the compute shader
        computeShader2D.SetBuffer(kernelHandle, "ResultBuffer", resultBuffer);
        computeShader2D.SetBuffer(kernelHandle, "moonPositions", moonPositionBuffer);
        computeShader2D.SetBuffer(kernelHandle, "moonMasses", moonMassBuffer);

        moonPositionBuffer.SetData(moonPositions);
        moonMassBuffer.SetData(moonMasses);

        // Set the actual moon count
        computeShader2D.SetInt("gridSize", sizeOfArray);

        // Dispatch the compute shader with the correct number of thread groups = [numthreads(32, 1, 1)]
        // int threadX = 32, threadY = 1, threadZ = 1;
        computeShader2D.Dispatch(kernelHandle, sizeOfArray / 64, 1, 1);

        // Read back the result from the GPU into an array
        Vector3[] resultData = new Vector3[sizeOfArray];
        resultBuffer.GetData(resultData);


        // 1D SOLUTION

        // Process the result as needed in your Start() method
        for (int i = 0; i < sizeOfArray; i++)
        {
            Vector3 force = resultData[i] * currentDeltaTime * forceMultiplier;
            Debug.DrawLine(moonPositions[i], moonPositions[i] + force * drawLine, Color.white, 0.25f);
            // Debug.Log("Force: " + force + "\ni = " + i, moonGameobjects[i]);
            moonRigidbodies[i].AddForce(force);
        }


        // 2D solution
        /*

        // Process the result as needed in your Start() method
        for (int i = 0; i < sizeOfArray; i++)
        {

            Vector3 forceSum = Vector3.zero;

            for (int j = 0; j < sizeOfArray; j++)
            {
                int index = j * sizeOfArray + i;
                forceSum += resultData[index];
            }



            Vector3 force = resultData[i] * currentDeltaTime * forceMultiplier;
            Debug.DrawLine(moonPositions[i], moonPositions[i] + force * 10000, Color.white, 0.25f);
            // Debug.Log("Force: " + force + "\ni = " + i, moonGameobjects[i]);
            moonRigidbodies[i].AddForce(force);
        }
        */


    }


    #endregion

    // ########################
    // ##### UI Inputs
    // ########################

    #region UI Inputs - button click, dropdown events, ...

    public void StartProcessClicked()
    {
        if (simulationInProgress)
        {
            // stop simulation
            simulationInProgress = false;

            // update UI
            canvasManager.UpdateButtonStartProcessText("Start simulation");
            canvasManager.buttonInstatiate.interactable = true;
        }
        else
        {
            // start simulation
            simulationInProgress = true;

            // update UI
            canvasManager.UpdateButtonStartProcessText("Stop simulation");
            canvasManager.buttonInstatiate.interactable = false;
        }
    }

    public void InstantiateClicked()
    {
        if (gameobjectInstantiated)
        {
            // gameobjects were created, time to destroy them
            gameobjectInstantiated = false;

            // destroy objects
            DestroyMoons();

            // update UI
            canvasManager.UpdateButtonInstantiateText("Spawn moons");
            canvasManager.inputField.interactable = true;
            canvasManager.buttonStartProcess.interactable = false;

        }
        else
        {
            // gameobjects were destroyed, we can create new now
            gameobjectInstantiated = true;

            // create objects
            InstantiateMoons();

            // update UI
            canvasManager.UpdateButtonInstantiateText("Destroy moons");
            canvasManager.inputField.interactable = false;
            canvasManager.buttonStartProcess.interactable = true;
        }
    }

    public void DropdownTimer(int value)
    {
        switch (value)
        {
            case 0:
                enumTimer = EnumTimer.OneSecond;
                break;
            case 1:
                enumTimer = EnumTimer.Update;
                break;
            case 2:
                enumTimer = EnumTimer.FixedUpdate;
                break;
            default:
                Debug.LogError("DropdownTimer: " + value, this);
                break;
        }
    }

    public void DropdownProcess(int value)
    {
        // clear saved simulation times
        m_ListAverageTime.Clear();

        switch (value)
        {
            case 0:
                enumProcessType = EnumProcessType.MainThread;
                break;
            case 1:
                enumProcessType = EnumProcessType.IJob;
                break;
            case 2:
                enumProcessType = EnumProcessType.ComputeShader_1D;
                break;
            case 3:
                enumProcessType = EnumProcessType.ComputeShader_2D;
                break;
            default:
                Debug.LogError("DropdownProcess: " + value,this);
                break;
        }
    }

    #endregion

    // ########################
    // ##### Misc
    // ########################

    public void GetCurrentDeltaTime()
    {
        currentDeltaTime = 10.0f;
        switch (enumTimer)
        {
            case EnumTimer.OneSecond:
                currentDeltaTime = 1.0f;
                break;
            case EnumTimer.Update:
                currentDeltaTime = Time.deltaTime;
                break;
            case EnumTimer.FixedUpdate:
                currentDeltaTime = Time.fixedDeltaTime;
                break;
        }

        // return currentDeltaTime;
    }

}
