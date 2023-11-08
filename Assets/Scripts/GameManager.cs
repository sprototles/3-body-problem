using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Burst;
using System.Collections;
using System.IO;
using UnityEditor;

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
    public bool simulationIsRunning = false;

    /// <summary>
    /// 
    /// </summary>
    public bool disableWarning = false;

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

    public int numThreadsX, numThreadsY;

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
    public ComputeShader computeShader;
    public ComputeShader computeShader2D;

    [HideInInspector] public ComputeShader computeShader1D_1;
    [HideInInspector] public ComputeShader computeShader1D_2;
    [HideInInspector] public ComputeShader computeShader1D_4;
    [HideInInspector] public ComputeShader computeShader1D_8;
    [HideInInspector] public ComputeShader computeShader1D_16;
    [HideInInspector] public ComputeShader computeShader1D_32;
    [HideInInspector] public ComputeShader computeShader1D_64;
    [HideInInspector] public ComputeShader computeShader1D_128;
    [HideInInspector] public ComputeShader computeShader1D_256;
    [HideInInspector] public ComputeShader computeShader1D_512;
    [HideInInspector] public ComputeShader computeShader1D_1024;

    [Header("Buffers")]
    public ComputeBuffer moonPositionBuffer;    // A buffer for moon positions
    public ComputeBuffer moonMassBuffer;        // A buffer for moon masses
    public ComputeBuffer resultBuffer;          // A buffer for the results

    public string computeShaderPath = "";

    [SerializeField]
    private string newText = "";

    [Header("Generated compute shader")]
    public ComputeShader generatedComputeShader;
    public string computeShaderString;
    public string computeShaderName;
    public string computeShaderResource;


    #endregion

    // Start is called before the first frame update
    void Start()
    {
        sizeOfArray = 1024;
        canvasManager.UpdateInputFieldText(sizeOfArray.ToString());

        forceMultiplier = 1000000f;

        enumTimer = EnumTimer.OneSecond;
        enumProcessType = EnumProcessType.MainThread;

        numThreadsX = 1;
        numThreadsY = 1;


        InvokeRepeating("OneSecondUpdate", 1.0f, 0.99f);


        // computeShaderPath = AssetDatabase.GetAssetPath(computeShader);
        computeShaderPath = "Assets/Resources/ComputeShader1D.compute";
        Debug.Log("Path of myObject: " + computeShaderPath);

        ReloadComputeShader();




    }

    // ########################
    // ##### Update
    // ########################

    #region Update, FixedUpdate, Repeat

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.Escape))
            StartSimulationClicked();


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
    #endregion

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

    /// <summary>
    /// update positions of moons
    /// </summary>
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
        DeleteGeneratedComputeShader();
        ReleaseBuffers();
    }

    #endregion

    // ########################
    // ##### Simulation
    // ########################

    #region RunSimulaiton

    private void RunSimulation()
    {
        // update myDeltaTime value
        GetCurrentDeltaTime();

        if (!simulationIsRunning)
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

        if (duration > 1000.0f)
            ShowWarning();

    }

    #endregion

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

    /// <summary>
    /// 
    /// </summary>
    private void ComputeShaderCalculations1D()
    {
        // computeShader = GetComputeShader();

        int kernelHandle = computeShader.FindKernel("CSMain");

        // Create the moon vector buffer
        moonPositionBuffer = new ComputeBuffer(sizeOfArray, sizeof(float) * 3);

        // Create the moon mass buffer
        moonMassBuffer = new ComputeBuffer(sizeOfArray, sizeof(float));

        // Create a result buffer
        resultBuffer = new ComputeBuffer(sizeOfArray, sizeof(float) * 3);

        // Set the result buffer in the compute shader
        computeShader.SetBuffer(kernelHandle, "ResultBuffer", resultBuffer);
        computeShader.SetBuffer(kernelHandle, "moonPositions", moonPositionBuffer);
        computeShader.SetBuffer(kernelHandle, "moonMasses", moonMassBuffer);

        moonPositionBuffer.SetData(moonPositions);
        moonMassBuffer.SetData(moonMasses);

        // Set the actual moon count
        computeShader.SetInt("gridSize", sizeOfArray);

        
        if (numThreadsX > sizeOfArray)
        {
            Debug.LogError("sizeOfArray must be bigger than threadSize",this);

            // turn off simulation
            StartSimulationClicked();
            ReleaseBuffers();
            return;
        }

        computeShader.Dispatch(kernelHandle, sizeOfArray / numThreadsX, 1, 1);

        // Read back the result from the GPU into an array
        Vector3[] resultData = new Vector3[sizeOfArray];
        resultBuffer.GetData(resultData);

        for (int i = 0; i < sizeOfArray; i++)
        {
            Vector3 force = resultData[i] * currentDeltaTime * forceMultiplier;
            moonRigidbodies[i].AddForce(force);

            Debug.DrawLine(moonPositions[i], moonPositions[i] + force * drawLine, Color.white, 0.25f);
        }
    }

    /// <summary>
    /// 
    /// </summary>
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
        computeShader2D.Dispatch(kernelHandle, sizeOfArray / 32, sizeOfArray / 32, 1);

        // Read back the result from the GPU into an array
        Vector3[] resultData = new Vector3[sizeOfArray];
        resultBuffer.GetData(resultData);

        // Process the result as needed in your Start() method
        for (int i = 0; i < sizeOfArray; i++)
        {
            Vector3 forceSum = Vector3.zero;

            for (int j = 0; j < sizeOfArray; j++)
            {
                int index = i * sizeOfArray + j;

                if (index >= sizeOfArray)
                    continue;

                forceSum += resultData[index];
            }

            Vector3 force = forceSum * currentDeltaTime * forceMultiplier;
            Debug.DrawLine(moonPositions[i], moonPositions[i] + force * drawLine, Color.white, 0.25f);
            // Debug.Log("Force: " + force + "\ni = " + i, moonGameobjects[i]);
            moonRigidbodies[i].AddForce(force);
        }

    }

    private void ReleaseBuffers()
    {
        moonPositionBuffer.Release();
        moonMassBuffer.Release();
        resultBuffer.Release();
    }

    private ComputeShader GetComputeShader()
    {
        switch (numThreadsX)
        {
            case 1: return computeShader1D_1;
            case 2: return computeShader1D_2;
            case 4: return computeShader1D_4;
            case 8: return computeShader1D_8;
            case 16: return computeShader1D_16;
            case 32: return computeShader1D_32;
            case 64: return computeShader1D_64;
            case 128: return computeShader1D_128;
            case 256: return computeShader1D_256;
            case 512: return computeShader1D_512;
            case 1024: return computeShader1D_1024;
            default:
                Debug.LogError("Incorrect numThreads: " + numThreadsX,this);
                return null;
        }
    }

    #endregion

    // ########################
    // ##### UI Inputs
    // ########################

    #region UI Inputs - button click, dropdown events, ...

    public void OkWarningClicked()
    {
        canvasManager.panelWarning.gameObject.SetActive(false);
    }

    public void DisableWarningClicked()
    {
        disableWarning = true;
        canvasManager.panelWarning.gameObject.SetActive(false);

    }

    public void StartSimulationClicked()
    {
        // start simulation
        simulationIsRunning = true;

        // update UI
        canvasManager.buttonSpawnObjects.interactable = false;
        canvasManager.buttonStartSimulation.gameObject.SetActive(false);
        canvasManager.buttonStopSimulation.gameObject.SetActive(true);
    }

    public void StopSimulationClicked()
    {
        // stop simulation
        simulationIsRunning = false;

        // update UI
        canvasManager.buttonSpawnObjects.interactable = true;
        canvasManager.buttonStartSimulation.gameObject.SetActive(true);
        canvasManager.buttonStopSimulation.gameObject.SetActive(false);
    }

    public void SpawnObjectsClicked()
    {
        // create objects
        InstantiateMoons();

        // update UI
        canvasManager.inputField.interactable = false;
        canvasManager.buttonSpawnObjects.gameObject.SetActive(false);
        canvasManager.buttonDestroyObjects.gameObject.SetActive(true);
        canvasManager.buttonStartSimulation.interactable = true;

    }

    public void DestroyObjectsClicked()
    {
        // create objects
        DestroyMoons();

        // update UI
        canvasManager.inputField.interactable = true;
        canvasManager.buttonSpawnObjects.gameObject.SetActive(true);
        canvasManager.buttonDestroyObjects.gameObject.SetActive(false);
        canvasManager.buttonStartSimulation.interactable = true;

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
                canvasManager.dropdownNumthreadX.interactable = false;
                break;
            case 1:
                enumProcessType = EnumProcessType.IJob;
                canvasManager.dropdownNumthreadX.interactable = false;
                break;
            case 2:
                enumProcessType = EnumProcessType.ComputeShader_1D;
                canvasManager.dropdownNumthreadX.interactable = true;
                break;
            case 3:
                enumProcessType = EnumProcessType.ComputeShader_2D;
                canvasManager.dropdownNumthreadX.interactable = true;
                break;
            default:
                Debug.LogError("DropdownProcess: " + value,this);
                break;
        }
    }


    public void DropdownNumthreadX(int value)
    {
        // clear saved simulation times
        m_ListAverageTime.Clear();
        numThreadsX = GetNumThread(value);
        // UpdateComputeShaderFile();
        ReloadComputeShader();
    }

    public void DropdownNumthreadY(int value)
    {
        // clear saved simulation times
        m_ListAverageTime.Clear();
        numThreadsY = GetNumThread(value);
        // UpdateComputeShaderFile();
        ReloadComputeShader();
    }

    public int GetNumThread(int value)
    {
        return (int)Mathf.Pow(2, value);
    }


    #endregion

    // ########################
    // ##### Misc
    // ########################


    private void ShowWarning()
    {
        if (disableWarning)
            return;

        // stop simulation
        StartSimulationClicked();
        canvasManager.panelWarning.gameObject.SetActive(true);

    }

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


    /// <summary>
    /// update compute shader file to make it "dynamic"
    /// </summary>
    private void UpdateComputeShaderFile()
    {
        if (File.Exists(computeShaderPath))
        {
            // Read all lines from the file into an array
            string[] lines = File.ReadAllLines(computeShaderPath);

            newText = "";

            for (int i = 0; i < lines.Length; i++)
            {
                // Check if the current line contains the search string
                if (lines[i].Contains("[numthreads("))
                {
                    // Replace the line with the replacement string
                    lines[i] = "[numthreads(" + numThreadsX + ", 1, 1)]";
                }

                // Check if the current line contains the search string
                if (lines[i].Contains("int startIndexX = id.x * gridSize / "))
                {
                    // Replace the line with the replacement string
                    lines[i] = "    int startIndexX = id.x * gridSize / " + numThreadsX + ";";
                }

                // Check if the current line contains the search string
                if (lines[i].Contains("int endIndexX = (id.x + 1) * gridSize / "))
                {
                    // Replace the line with the replacement string
                    lines[i] = "    int endIndexX = (id.x + 1) * gridSize / " + numThreadsY + ";";
                }

                newText += lines[i] + "\n";
            }

            // Write the modified lines back to the file
            File.WriteAllLines(computeShaderPath, lines);
            Debug.Log(lines);
        }
        else
        {
            Debug.LogError("File not found: " + computeShaderPath);
        }
    }

    private void ReloadComputeShader()
    {
        // AssetDatabase.Refresh();

        /*
        computeShader = null;
        computeShader = Resources.Load<ComputeShader>("ComputeShader1D");
        if (!computeShader)
        { 
            Debug.LogError("Compute shader not found\npath = " + computeShaderPath, this);
        }
        else
        {
            Debug.Log("computeShader reloaded = " + computeShader, this);
        }
        */
        computeShader = GenerateComputeShader();
    }


    private ComputeShader GenerateComputeShader()
    {
        DeleteGeneratedComputeShader();
        // set strings
        computeShaderName = "GeneratedShader_" + numThreadsX.ToString() + "_" + numThreadsY.ToString();
        computeShaderResource = "Assets/Resources/" + computeShaderName + ".compute";

        // create new compute shader text
        computeShaderString = GenerateComputeShaderCode();

        // save it to compute shader file
        File.WriteAllText(computeShaderResource, computeShaderString);

        // refresh assets so unity can find it during runtime
        // AssetDatabase.Refresh();

        // load new generated compute shader
        generatedComputeShader = Resources.Load<ComputeShader>(computeShaderName);

        return generatedComputeShader;
    }

    private void DeleteGeneratedComputeShader()
    {
        Debug.Log("DeleteGeneratedComputeShader\nFile: " + computeShaderResource ,this);
        // delete old compute shader if exist
        if (computeShaderResource != "")
        {
            if (File.Exists(computeShaderResource))
            {
                // AssetDatabase.DeleteAsset(computeShaderResource);
                File.Delete(computeShaderResource);
            }
        }
    }

    private string GenerateComputeShaderCode()
    {
        // Generate the Compute Shader code based on parameters
        string shaderCode = $@"
#pragma kernel CSMain

int gridSize;
RWStructuredBuffer<float3> moonPositions; // Each moon vector is a float4
RWStructuredBuffer<float> moonMasses; // Array of moon masses
RWStructuredBuffer<float3> ResultBuffer;

[numthreads({numThreadsX}, {numThreadsY}, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{{
    int startIndexX = id.x * gridSize / {numThreadsX};
    int endIndexX = (id.x + 1) * gridSize / {numThreadsX};

    int startIndexY = id.y * gridSize / {numThreadsY};
    int endIndexY = (id.y + 1) * gridSize / {numThreadsY};

    for (int i = startIndexX; i < endIndexX; i++)
    {{
        // skip if iterating too much
        if (i >= gridSize)
        {{
            continue;
        }}
        
        float3 forceOnMoon = float3(0, 0, 0);
        
        for (int j = 0; j < gridSize; j++)
        {{                        
            float3 v3Distance = moonPositions[i] - moonPositions[j];
            float distance = sqrt(dot(v3Distance, v3Distance)); // Calculate distance
            if (distance > 0.0)
            {{
                float forceForce = 0.00000000006675f * ((moonMasses[i] * moonMasses[j]) / distance);
                float3 dir = normalize(moonPositions[j] - moonPositions[i]);
                forceOnMoon += dir * forceForce;
            }}
        }}
        
        ResultBuffer[i] = forceOnMoon;
    }}
}}
        ";

        return shaderCode;
    }


    IEnumerator ExampleCoroutine()
    {
        //yield on a new YieldInstruction that waits for 5 seconds.
        yield return new WaitForSeconds(5);
    }

}
