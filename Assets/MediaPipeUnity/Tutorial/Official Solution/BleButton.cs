using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BleButton : MonoBehaviour
{
    public GameObject Blemain;
    bool show=false;
    public void Click()
    {
        if(show == false)
        {
            Blemain.SetActive(true);
            show = true;
        }
        else
        {
            Blemain.SetActive(false);
            show = false;
        }
    }
}
