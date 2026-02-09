using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class MediaPipeReceiver : MonoBehaviour
{
    public int port = 5055;

    UdpClient udp;
    Thread thread;

    Dictionary<string, Vector3> landmarks = new();
    Dictionary<string, Vector3> buffer = new();

    object lockObj = new object();

    void Start()
    {
        udp = new UdpClient(port);
        thread = new Thread(ReceiveData);
        thread.IsBackground = true;
        thread.Start();
        Debug.Log("MediaPipe Receiver started");
    }

    void Update()
    {
        // copy จาก thread → main thread
        lock (lockObj)
        {
            foreach (var kv in buffer)
                landmarks[kv.Key] = kv.Value;
        }
    }

    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            byte[] data = udp.Receive(ref anyIP);
            string json = Encoding.UTF8.GetString(data);

            ParseData(json);
        }
    }

    void ParseData(string data)
    {
        var temp = new Dictionary<string, Vector3>();

        // Format: KEY:x,y,z|KEY:x,y,z|...
        var lines = data.Split('|');

        foreach (var line in lines)
        {
            var parts = line.Split(':');
            if (parts.Length != 2) continue;

            string key = parts[0];
            string[] values = parts[1].Split(',');

            if (values.Length >= 3 &&
                float.TryParse(values[0], out float x) &&
                float.TryParse(values[1], out float y) &&
                float.TryParse(values[2], out float z))
            {
                temp[key] = new Vector3(x, y, z);
            }
        }

        lock (lockObj)
        {
            buffer = temp;
        }
    }

    public bool TryGet(string key, out Vector3 value)
    {
        return landmarks.TryGetValue(key, out value);
    }
}
