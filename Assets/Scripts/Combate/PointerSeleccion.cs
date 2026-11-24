using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Sincroniza el resaltado del mouse con la selección del menú.</summary>
public sealed class PointerSeleccion : MonoBehaviour, IPointerEnterHandler
{
    private Action seleccionar;
    private Button boton;

    public void Configurar(Action accionSeleccion, Button button)
    {
        seleccionar = accionSeleccion;
        boton = button;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (boton != null && boton.interactable)
            seleccionar?.Invoke();
    }
}
