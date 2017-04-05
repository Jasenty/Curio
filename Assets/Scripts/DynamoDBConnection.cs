using Amazon;
using Amazon.CognitoIdentity;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//Fix crashing due to loss of signal

public class DynamoDBConnection : MonoBehaviour
{
    public Text m_CanvasText;
    public Text m_SuccessText;
    public float m_UpdateSpeed = 5f;

    public float m_LocationRadius = 0.0002f; // to the nearest 11.1 meters

    private DynamoDBContext Context;

    private float m_Latitude = 0;
    private float m_Longitude = 0;
    private float m_Altiude = 0;
    private int m_TableLength = 0;
    private float m_CompassDegree = 0;
    // Use this for initialization
    void Start()
    {
        m_TableLength = 0;
        UnityInitializer.AttachToGameObject(this.gameObject);

        var credentials = new CognitoAWSCredentials("us-east-1:4a6bd71a-2349-4b3f-9f10-e2865187b87a", RegionEndpoint.USEast1);
        AmazonDynamoDBClient client = new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);
        Context = new DynamoDBContext(client);

        var request = new DescribeTableRequest
        {
            TableName = @"AppathonTable"
        };
        client.DescribeTableAsync(request, (result) =>
        {
            if (result.Exception != null)
            {
                Debug.Log(result.Exception);
                return;
            }
            var response = result.Response;
            TableDescription description = response.Table;
            Debug.Log("# of items: " + response.Table.ItemCount + "\n");
            m_TableLength = (int)response.Table.ItemCount;
            Debug.Log("Provision Throughput (reads/sec): " +
                description.ProvisionedThroughput.ReadCapacityUnits + "\n");
            //StartCoroutine(CheckTableLength(Context));

        }, null);

        GetTableLength(Context); // first gets table length then calls the Acquring function, this should only need to happen once
        //StartCoroutine(CheckTableLength(Context));
        //SimpleAddObjectToDB(Context);
        //SimpleRetrieveObject(Context);
        //DeleteObjectFromDB(Context);
        StartCoroutine(GetLocationWait());
        //AddObjectToDB(Context);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void AddPhoneLocationToDB()
    {
        AddObjectToDB(Context);
    }

    private IEnumerator GetLocationWait()
    {
        yield return new WaitForSeconds(m_UpdateSpeed);
        Debug.Log("Waited");
        StartCoroutine(GetLocation());
    }

    private IEnumerator GetLocation()
    {
        // First, check if user has location service enabled
        if (!Input.location.isEnabledByUser)
            yield break;

        // Start service before querying location
        Input.location.Start();
        Input.compass.enabled = true;
        Input.gyro.enabled = true;

        // Wait until service initializes
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        // Service didn't initialize in 20 seconds
        if (maxWait < 1)
        {
            print("Timed out");
            yield break;
        }

        // Connection has failed
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            print("Unable to determine device location");
            yield break;
        }
        else
        {
            // Access granted and location value could be retrieved
            m_CanvasText.text = "Location: \n" + "Latitude: " + Input.location.lastData.latitude + "\n" + "Longitude: " + Input.location.lastData.longitude + "\n" + "Altitude: " + Input.location.lastData.altitude;
            m_Latitude = Input.location.lastData.latitude;
            m_Longitude = Input.location.lastData.longitude;
            m_Altiude = Input.location.lastData.altitude;
            m_CompassDegree = Input.compass.magneticHeading;
        }

        // Stop service if there is no need to query location updates continuously
        Input.location.Stop();
        RetrieveDBObjectLocation(Context);
        StartCoroutine(GetLocationWait());

    }

    private void SimpleAddObjectToDB(DynamoDBContext context)
    {
        PlacedObject myObj = new PlacedObject
        {
            UserName = 3,
            CompassAngle = m_TableLength,
            Latitude = 43.091f,
            Longitude = -115.234f,
            Altitude = 0
        };

        // Save the book.
        context.SaveAsync(myObj, (result) =>
        {
            if (result.Exception == null)
                Debug.Log("Object Saved");
        });
    }

    private void AddCurrentObjectToDB(DynamoDBContext context)
    {
        PlacedObject myObj = new PlacedObject
        {
            UserName = m_TableLength,
            CompassAngle = m_CompassDegree,
            Latitude = m_Latitude,
            Longitude = m_Longitude,
            Altitude = m_Altiude
        };

        // Save the book.
        context.SaveAsync(myObj, (result) =>
        {
            if (result.Exception == null)
                Debug.Log("Object Saved");
        });
    }

    private void RetrieveDBObjectLocation(DynamoDBContext context)
    {
        m_SuccessText.text = "";
        bool foundObject = false;
        for (int j = 1; j <= m_TableLength; j++) 
        {
            if (foundObject)
            {
                break;
            }
            PlacedObject placedObject = new PlacedObject();
            context.LoadAsync<PlacedObject>(j,
                (AmazonDynamoDBResult<PlacedObject> result) =>
                {
                    if (result.Exception != null)
                    {
                        Debug.LogException(result.Exception);
                        return;
                    }
                    placedObject = result.Result;
                    float latitude = placedObject.Latitude;
                    float longitude = placedObject.Longitude;
                    float compassDeg = placedObject.CompassAngle;
                    //m_SuccessText.text = "Our Latitude: " + m_Latitude + "\nDB latitude: " + latitude;
                
                    if (latitude >= m_Latitude - m_LocationRadius && latitude <= m_Latitude + m_LocationRadius)
                    {
                        //m_SuccessText.text = "Almost there";
                        if (longitude >= m_Longitude - m_LocationRadius && longitude <= m_Longitude + m_LocationRadius)
                        {
                            m_SuccessText.text = "In Right Area! \nThis is your number: " + placedObject.UserName + "\nTable Length: " + m_TableLength.ToString();
                            foundObject = true;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                    Debug.Log(latitude);

                }, null);
        }
    }

    private void SimpleRetrieveObject(DynamoDBContext context)
    {
        PlacedObject placedObject = new PlacedObject();
        context.LoadAsync<PlacedObject>(2,
            (AmazonDynamoDBResult<PlacedObject> result) =>
            {
                if (result.Exception != null)
                {

                    Debug.LogException(result.Exception);
                    return;
                }
                placedObject = result.Result;

            }, null);
    }
        

    private void DeleteObjectFromDB(DynamoDBContext context)
    {
        context.DeleteAsync<PlacedObject>(1, (res) =>
        {
            if (res.Exception == null)
            {
                context.LoadAsync<PlacedObject>(1, (result) =>
                {
                    PlacedObject deletedBook = result.Result;
                    if (deletedBook == null)
                        Debug.Log("Object Deleted");
                });
            }
        });
    }

    private void AddObjectToDB(DynamoDBContext context)
    {
        // Retrieve the book.
        PlacedObject placedObject = null;
        context.LoadAsync<PlacedObject>(1, (result) => // one for starter object meant to hold object count
        {
            if (result.Exception == null)
            {
                placedObject = result.Result as PlacedObject;
                // Update few properties.
                placedObject.CompassAngle += 1; // to add to the total amount of objects placed in world
                m_TableLength = (int)placedObject.CompassAngle;
                context.SaveAsync<PlacedObject>(placedObject, (res) =>
                {
                    if (res.Exception == null)
                    {
                        Debug.Log("Book updated");
                        AddCurrentObjectToDB(context);
                    }
                });
            }
        });
    }

    private void GetTableLength(DynamoDBContext context)
    {
        PlacedObject placedObject = null;
        context.LoadAsync<PlacedObject>(1, (result) => // one for starter object meant to hold object count
        {
            if (result.Exception == null)
            {
                placedObject = result.Result as PlacedObject;
                // Update few properties.
                //placedObject.CompassAngle += 1; // to add to the total amount of objects placed in world
                m_TableLength = (int)placedObject.CompassAngle;
                context.SaveAsync<PlacedObject>(placedObject, (res) =>
                {
                    if (res.Exception == null)
                    {
                        Debug.Log("Table Length Acquired");
                    }
                });
            }
        });
    }
}

[DynamoDBTable("AppathonTable")]
public class PlacedObject
{
    [DynamoDBHashKey]   // Hash key.
    public int UserName { get; set; }
    [DynamoDBProperty]
    public float CompassAngle { get; set; }
    [DynamoDBProperty]
    public float Latitude { get; set; }
    [DynamoDBProperty]
    public float Longitude { get; set; }
    [DynamoDBProperty]
    public float Altitude { get; set; }
}