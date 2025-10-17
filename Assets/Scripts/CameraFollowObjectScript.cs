using System.Collections;
using UnityEngine;

public class CameraFollowObject : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _playerTransform;

    [Header("Flip Rotation Stats")]
    [SerializeField] private float _flipYRotationTime = 0.5f;
    [SerializeField] private Vector3 offsetRight = new Vector3(2f, 0f, -10f);
    [SerializeField] private Vector3 offsetLeft = new Vector3(-2f, 0f, -10f);

    private Coroutine _turnCoroutine;
    private PlayerController _player;
    private bool _isFacingRight;

    private void Awake()
    {
        _player = _playerTransform.GetComponent<PlayerController>();
        _isFacingRight = _player.IsFacingRight;
    }

    private void Update()
    {
        // Actualiza dirección si cambió
        if (_isFacingRight != _player.IsFacingRight)
        {
            _isFacingRight = _player.IsFacingRight;
            CallTurn();
        }

        // Offset dinámico según dirección
        Vector3 offset = _isFacingRight ? offsetRight : offsetLeft;
        transform.position = _playerTransform.position + offset;
    }

    void CallTurn()
    {
        if (_turnCoroutine != null)
            StopCoroutine(_turnCoroutine);

        _turnCoroutine = StartCoroutine(FlipLerp());
    }

    private IEnumerator FlipLerp()
    {
        float startRotation = transform.localEulerAngles.y;
        float endRotationAmount = _isFacingRight ? 0f : 180f;
        float elapsedTime = 0f;

        while (elapsedTime < _flipYRotationTime)
        {
            elapsedTime += Time.deltaTime;
            float yRotation = Mathf.Lerp(startRotation, endRotationAmount, elapsedTime / _flipYRotationTime);
            transform.rotation = Quaternion.Euler(0, yRotation, 0);
            yield return null;
        }
    }
}