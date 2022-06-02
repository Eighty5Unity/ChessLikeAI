using System;
using UnityEngine;
using UnityEngine.UI;

public class UIView : MonoBehaviour
{
    [SerializeField] Button predictUseThreading;
    [SerializeField] Button predictWithoutThreading;
    [SerializeField] Button nextMove;

    public event Action predictUseThreadingButtonClick;
    public event Action predictWithoutThreadingButtonClick;
    public event Action nextMoveButtonClick;

    private void Awake()
    {
        predictUseThreading.onClick.AddListener(UseThreadingButton);
        predictWithoutThreading.onClick.AddListener(WithoutThreadingButton);
        nextMove.onClick.AddListener(NextMoveButton);
    }

    void UseThreadingButton()
    {
        predictUseThreadingButtonClick?.Invoke();
    }

    void WithoutThreadingButton()
    {
        predictWithoutThreadingButtonClick?.Invoke();
    }

    void NextMoveButton()
    {
        nextMoveButtonClick?.Invoke();
    }
}
