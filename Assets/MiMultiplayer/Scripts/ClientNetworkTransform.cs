using UnityEngine;
using Unity.Netcode.Components;
using static UnityEngine.UI.GridLayoutGroup;

public class ClientNetworkTransform : NetworkTransform
{

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
