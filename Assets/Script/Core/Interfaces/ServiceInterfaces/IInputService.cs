using System;
using UnityEngine;
using YokaiNoMori.Interface;

public interface IInputService 
{
    event Action<CaseView> OnCellHoverChanged;
    event Action<CaseView> OnCellLeftClicked;
    event Action<CaseView> OnCellRightClicked;
    event Action<IPawn> OnReservePawnClicked;
}
