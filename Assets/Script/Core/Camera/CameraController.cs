using System.Collections;
using UnityEngine;
using Tools;

public class CameraController : MonoBehaviour, IEventListener<CameraEvent>
{
    private float _fixedX;
    private float _fixedY;
    private float _currentZ;
    private Coroutine _rotationCoroutine;

    private void Start()
    {
        _fixedX = transform.eulerAngles.x;
        _fixedY = transform.eulerAngles.y;
        _currentZ = transform.eulerAngles.z;
        ApplyRotation(_currentZ);
    }

    private void OnEnable() => this.EventStartListening<CameraEvent>();
    private void OnDisable() => this.EventStopListening<CameraEvent>();

    public void OnEvent(CameraEvent e)
    {
        switch (e.EventType)
        {
            case CameraEventType.RotateTo:
                if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
                _rotationCoroutine = StartCoroutine(RotateTo(e.TargetZ, e.Duration, e.TweenType));
                break;

            case CameraEventType.SnapTo:
                if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
                _currentZ = e.TargetZ;
                ApplyRotation(_currentZ);
                break;
        }
    }

    private IEnumerator RotateTo(float targetZ, float duration, MyTween.TweenType tweenType)
    {
        float elapsed = 0f;
        float startZ = _currentZ;

        while (elapsed < duration)
        {
            float t = MyTween.Evaluate(elapsed / duration, tweenType);
            _currentZ = Mathf.Lerp(startZ, targetZ, t);
            ApplyRotation(_currentZ);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _currentZ = targetZ;
        ApplyRotation(_currentZ);
        _rotationCoroutine = null;
    }

    private void ApplyRotation(float z)
        => transform.rotation = Quaternion.Euler(_fixedX, _fixedY, z);
}