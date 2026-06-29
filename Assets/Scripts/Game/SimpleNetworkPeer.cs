using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public sealed class SimpleNetworkPeer : MonoBehaviour
{
    private readonly List<TcpClient> clients = new List<TcpClient>();
    private TcpListener listener;
    private TcpClient client;
    private readonly object statusLock = new object();
    private string status = "Offline";

    public string Status
    {
        get
        {
            lock (statusLock)
                return status;
        }
    }

    private void Start()
    {
        if (NetworkSessionConfig.Role == NetworkRole.Server)
            StartServer();
        else if (NetworkSessionConfig.Role == NetworkRole.Client)
            StartClient();
        else
            SetStatus("Offline run");
    }

    private void Update()
    {
        if (listener == null)
            return;

        while (listener.Pending())
        {
            TcpClient accepted = listener.AcceptTcpClient();
            clients.Add(accepted);
            SetStatus($"Server listening on :{NetworkSessionConfig.Port} | clients {clients.Count}");
        }
    }

    private void OnDestroy()
    {
        listener?.Stop();
        listener = null;

        client?.Close();
        client = null;

        foreach (TcpClient accepted in clients)
            accepted.Close();

        clients.Clear();
    }

    private void StartServer()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, NetworkSessionConfig.Port);
            listener.Start();
            SetStatus($"Server listening on :{NetworkSessionConfig.Port} | clients 0");
        }
        catch (Exception ex)
        {
            SetStatus($"Server failed: {ex.Message}");
        }
    }

    private void StartClient()
    {
        client = new TcpClient();
        SetStatus($"Connecting to {NetworkSessionConfig.Address}:{NetworkSessionConfig.Port}");

        try
        {
            client.BeginConnect(NetworkSessionConfig.Address, NetworkSessionConfig.Port, OnClientConnected, client);
        }
        catch (Exception ex)
        {
            SetStatus($"Client failed: {ex.Message}");
        }
    }

    private void OnClientConnected(IAsyncResult result)
    {
        try
        {
            var tcpClient = (TcpClient)result.AsyncState;
            tcpClient.EndConnect(result);
            SetStatus($"Connected to {NetworkSessionConfig.Address}:{NetworkSessionConfig.Port}");
        }
        catch (Exception ex)
        {
            SetStatus($"Client failed: {ex.Message}");
        }
    }

    private void SetStatus(string value)
    {
        lock (statusLock)
            status = value;
    }
}
