using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Board board;
    [SerializeField] private float dragThreshold;
    
    private Camera _camera;

    private bool _swap;
    private bool _pressing;
    private Vector3 _pressWorld;
    private (int x, int y) _pressXY;

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        if (board == null || _camera == null) return;
        if (board.isProcessing) return;

        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) OnPointerDown(touch.position);
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) OnPointerDrag(touch.position);
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) OnPointerUp();
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) OnPointerDown(Input.mousePosition);
            else if (Input.GetMouseButton(0)) OnPointerDrag(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) OnPointerUp();
        }
    }

    private void OnPointerDown(Vector3 screenPos)
    {
        var worldPos = _camera.ScreenToWorldPoint(screenPos);
        worldPos.z = 0f;

        var (x, y) = board.WorldToXY(worldPos);
        if (!board.HasGem(x, y))
        {
            _pressing = false;
            return;
        }

        _swap = false;
        _pressing = true;
        _pressWorld = worldPos;
        _pressXY = (x, y);
    }

    private void OnPointerDrag(Vector3 screenPos)
    {
        if (!_pressing || _swap) return;

        var worldPos = _camera.ScreenToWorldPoint(screenPos);
        worldPos.z = 0f;

        Vector3 drag = worldPos - _pressWorld;
        if (drag.magnitude < dragThreshold) return;
        
        Vector2 dir = Mathf.Abs(drag.x) > Mathf.Abs(drag.y)
            ? (drag.x > 0 ? Vector2.right : Vector2.left)
            : (drag.y > 0 ? Vector2.up    : Vector2.down);

        var target = (_pressXY.x + (int)dir.x, _pressXY.y + (int)dir.y);
        if (!board.HasGem(target.Item1, target.Item2)) return;
        if (!board.IsAdjacent(_pressXY, target)) return;
        
        _swap = true;
        board.Swap(_pressXY, target);
    }

    private void OnPointerUp()
    {
        _pressing = false;
        _swap = false;
    }
}
