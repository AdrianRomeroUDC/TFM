using UnityEngine;
using TMPro; // Obligatorio para usar TextMeshPro
using System;

public class UI_StockController : MonoBehaviour
{
    [Header("--- Componente Reloj ---")]
    [SerializeField] private TextMeshProUGUI textoReloj;

    void Update()
    {
        if (textoReloj != null)
        {
            // Captura la fecha y hora del sistema operativo frame a frame
            textoReloj.text = DateTime.Now.ToString("dd/MM/yyyy   HH:mm:ss");
        }
    }
}
