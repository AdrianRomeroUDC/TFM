using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class ControladorCintaSLD_mqtt : MonoBehaviour
{
    private MqttClient client;
    public string brokerHost = "4ca80baa3731405580bfa27dc37e6665.s1.eu.hivemq.cloud";
    public string topicBelt = "f/sld/belt";

    [Header("Configuración de Movimiento")]
    public float velocidadActual = 0f;
    public float multiplicadorVelocidad = 0.001f;

    private List<Transform> eslabonesOrdenados = new List<Transform>();
    private Vector3[] posRailes;
    private Quaternion[] rotRailes;
    private float progresoCiclo = 0f;
    private float velocidadPendiente = 0f;

    void Start()
    {
        OrdenarEslabonesPorCercania();

        posRailes = new Vector3[eslabonesOrdenados.Count];
        rotRailes = new Quaternion[eslabonesOrdenados.Count];

        for (int i = 0; i < eslabonesOrdenados.Count; i++)
        {
            posRailes[i] = eslabonesOrdenados[i].localPosition;
            rotRailes[i] = eslabonesOrdenados[i].localRotation;
        }

        Connect();
    }

    void OrdenarEslabonesPorCercania()
    {
        List<Transform> sinOrdenar = new List<Transform>();
        foreach (Transform t in transform) sinOrdenar.Add(t);

        if (sinOrdenar.Count == 0) return;

        // Empezamos por el primero que encontremos
        Transform actual = sinOrdenar[0];
        eslabonesOrdenados.Add(actual);
        sinOrdenar.RemoveAt(0);

        // Buscamos siempre el más cercano al anterior para formar la cadena
        while (sinOrdenar.Count > 0)
        {
            Transform masCercano = sinOrdenar
                .OrderBy(t => Vector3.Distance(t.localPosition, actual.localPosition))
                .First();

            eslabonesOrdenados.Add(masCercano);
            sinOrdenar.Remove(masCercano);
            actual = masCercano;
        }
    }

    void Update()
    {
        velocidadActual = velocidadPendiente;

        if (velocidadActual > 0 && eslabonesOrdenados.Count > 0)
        {
            progresoCiclo += velocidadActual * multiplicadorVelocidad * Time.deltaTime;
            if (progresoCiclo >= 1f) progresoCiclo -= 1f;

            for (int i = 0; i < eslabonesOrdenados.Count; i++)
            {
                int sigIdx = (i + 1) % eslabonesOrdenados.Count;
                eslabonesOrdenados[i].localPosition = Vector3.Lerp(posRailes[i], posRailes[sigIdx], progresoCiclo);
                eslabonesOrdenados[i].localRotation = Quaternion.Slerp(rotRailes[i], rotRailes[sigIdx], progresoCiclo);
            }
        }
    }

    void OnMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message).Trim();
        if (float.TryParse(msg, out float val)) velocidadPendiente = val;
    }

    void Connect()
    {
        try
        {
            client = new MqttClient(brokerHost, 8883, true, null, null, MqttSslProtocols.TLSv1_2);
            client.MqttMsgPublishReceived += OnMessageReceived;
            client.Connect(Guid.NewGuid().ToString(), "LearningFactory", "Fischertechnik1");
            client.Subscribe(new string[] { topicBelt }, new byte[] { 0 });
        }
        catch (Exception ex) { Debug.LogError(ex.Message); }
    }
}