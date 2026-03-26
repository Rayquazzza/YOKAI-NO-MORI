using System;
using UnityEngine;

public interface IInputService 
{
    event Action<BoardCaseView> OnCellHoverChanged;
    event Action<BoardCaseView> OnCellLeftClicked;
    event Action<BoardCaseView> OnCellRightClicked;
}
