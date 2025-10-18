using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class Board : MonoBehaviour
{
    public bool isProcessing;

    public bool IsInBounds(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;
    public bool HasCell(int x, int y) => IsInBounds(x, y) && _hasCell[x, y];
    public bool HasGem(int x, int y) => HasCell(x, y) && _gems[x, y] != null;
    
    public (int x, int y) WorldToXY(Vector3 worldPos)
    {
        var cell = grid.WorldToCell(worldPos);
        int x = cell.x - _originCell.x;
        int y = cell.y - _originCell.y;
        return (x, y);
    }

    public Vector3 XYToWorld(int x, int y)
    {
        var cell = new Vector3Int(x + _originCell.x, y + _originCell.y, 0);
        return grid.GetCellCenterWorld(cell);
    }

    public bool IsAdjacent((int x, int y) a, (int x, int y) b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) == 1;
    }
    
    public void Swap((int x, int y) a, (int x, int y) b)
    {
        if (isProcessing) return;

        StartCoroutine(CoSwap(a, b));
    }
    
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap tileMap;

    [SerializeField] private Gem gemPrefab;
    [SerializeField] private Sprite[] sprites;
    
    private Gem[,] _gems;
    private bool[,] _hasCell;
    
    private Vector3Int _originCell;
    private int _width, _height;

    private ObjectPool<Gem> gemPool;

    private void Awake()
    {
        gemPool = new ObjectPool<Gem>(
            createFunc: () =>
            {
                var gem = Instantiate(gemPrefab);
                return gem;
            },
            actionOnGet: gem =>
            {
                gem.gameObject.SetActive(true);
            },
            actionOnRelease: gem =>
            {
                gem.gameObject.SetActive(false);
            }
        );
    }

    private void Start()
    {
        CreateBoard();
    }
    
    private int GetTopCellY(int x)
    {
        for (int y = _height - 1; y >= 0; y--)
        {
            if (HasCell(x, y)) return y;
        }

        return -1;
    }
    
    private void AvoidImmediateMatch(Gem gem, int x, int y, int maxTries = 10)
    {
        for (int i = 0; i < maxTries; i++)
        {
            int r = Random.Range(0, sprites.Length);
            gem.Set((GemType)r, sprites[r]);
            _gems[x, y] = gem;
            if (FindMatchesAt(x, y).Count == 0) return;
        }
    }

    private void CreateBoard()
    {
        tileMap.CompressBounds();
        var bounds = tileMap.cellBounds;

        _originCell = bounds.min;
        _width  = bounds.size.x;
        _height = bounds.size.y;

        _gems = new Gem[_width, _height];
        _hasCell = new bool[_width, _height];

        foreach (var cell in bounds.allPositionsWithin)
        {
            if(!tileMap.HasTile(cell)) continue;
            int x = cell.x - _originCell.x;
            int y = cell.y - _originCell.y;
            
            if(!IsInBounds(x, y)) continue;
            _hasCell[x, y] = true;
            
            Vector3 worldPos = grid.GetCellCenterWorld(cell);

            var gem = gemPool.Get();
            gem.transform.position = worldPos;
            gem.transform.rotation = Quaternion.identity;
            
            AvoidImmediateMatch(gem, x, y);
            
            _gems[x, y] = gem;
        }

        if(!CanMakeMatches()) RemoveBoard();
    }

    private void RemoveBoard()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (!HasGem(x, y)) continue;
                var gem = _gems[x, y];
                
                gemPool.Release(gem);
                _gems[x, y] = null;
            }
        }
        
        CreateBoard();
    }

    private HashSet<Vector2Int> FindAllMatches()
    {
        var matches = new HashSet<Vector2Int>();

        for (int y = 0; y < _height; y++)
        {
            int x = 0;
            while (x < _width)
            {
                if (!HasGem(x, y))
                {
                    x++;
                    continue;
                }

                GemType type = _gems[x, y].type;
                int start = x;
                int length = 1;
                int next = x + 1;

                while (next < _width && HasGem(next, y) && _gems[next, y].type == type)
                {
                    length++;
                    next++;
                }

                if (length >= 3)
                {
                    for (int i = start; i < start + length; i++)
                    {
                        matches.Add(new Vector2Int(i, y));
                    }
                }

                x = next;
            }
        }

        for (int x = 0; x < _width; x++)
        {
            int y = 0;
            while (y < _height)
            {
                if (!HasGem(x, y))
                {
                    y++;
                    continue;
                }

                GemType type = _gems[x, y].type;
                int start = y;
                int length = 1;
                int next = y + 1;

                while (next < _height && HasGem(x, next) && _gems[x, next].type == type)
                {
                    length++;
                    next++;
                }

                if (length >= 3)
                {
                    for (int j = start; j < start + length; j++)
                    {
                        matches.Add(new Vector2Int(x, j));
                    }
                }

                y = next;
            }
        }

        return matches;
    }

    private HashSet<Vector2Int> FindMatchesAt(int x, int y)
    {
        var matches = new HashSet<Vector2Int>();
        if (!HasGem(x, y)) return matches;

        GemType type = _gems[x, y].type;

        int left = x - 1;
        while (left >= 0  && HasGem(left, y) && _gems[left, y].type == type) left--;
        int right = x + 1;
        while (right < _width && HasGem(right, y) && _gems[right, y].type == type) right++;

        int horizontal = (right - 1) - (left + 1) + 1;
        if (horizontal >= 3)
        {
            for (int i = left + 1; i <= right - 1; i++)
            {
                matches.Add(new Vector2Int(i, y));
            }
        }
        
        int down = y - 1;
        while (down >= 0  && HasGem(x, down) && _gems[x, down].type == type) down--;
        int up = y + 1;
        while (up < _height  && HasGem(x, up) && _gems[x, up].type == type) up++;

        int vertical = (up - 1) - (down + 1) + 1;
        if (vertical >= 3)
        {
            for (int j = down + 1; j <= up - 1; j++)
            {
                matches.Add(new Vector2Int(x, j));
            }
        }

        return matches;
    }

    private HashSet<int> ClearMatches(HashSet<Vector2Int> matches)
    {
        var clearCol = new HashSet<int>();
        foreach (var match in matches)
        {
            int x = match.x;
            int y = match.y;
            if (!HasGem(x, y)) continue;
            
            gemPool.Release(_gems[x, y]);
            _gems[x, y] = null;

            clearCol.Add(x);
        }

        return clearCol;
    }

    private bool CanMakeMatches()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (!HasGem(x, y)) continue;

                if (x + 1 < _width && HasGem(x + 1, y))
                {
                    if (_gems[x, y].type != _gems[x + 1, y].type)
                    {
                        var a = _gems[x, y];
                        var b = _gems[x + 1, y];

                        _gems[x, y] = b; _gems[x + 1, y] = a; 
                        bool ok = FindMatchesAt(x, y).Count > 0 || FindMatchesAt(x + 1, y).Count > 0;
                        _gems[x, y] = a; _gems[x + 1, y] = b;

                        if (ok) return true;
                    }
                }
                
                if (y + 1 < _height && HasGem(x, y + 1))
                {
                    if (_gems[x, y].type != _gems[x, y + 1].type)
                    {
                        var a = _gems[x, y];
                        var b = _gems[x, y + 1];

                        _gems[x, y] = b; _gems[x, y + 1] = a;
                        bool ok = FindMatchesAt(x, y).Count > 0 || FindMatchesAt(x, y + 1).Count > 0;
                        _gems[x, y] = a; _gems[x, y + 1] = b;

                        if (ok) return true;
                    }
                }
            }
        }
        return false;
    }
    
    private IEnumerator CoSwap((int x, int y) a, (int x, int y) b)
    {
        isProcessing = true;

        yield return StartCoroutine(CoSwaping(a, b));
        var matches = FindMatchesAt(a.x, a.y);
        matches.UnionWith(FindMatchesAt(b.x, b.y));

        if (matches.Count == 0)
        {
            yield return StartCoroutine(CoSwaping(a, b));
            isProcessing = false;
            yield break;
        }

        while (matches.Count > 0)
        {
            var clearCol = ClearMatches(matches);
            
            yield return StartCoroutine(CoDrop(clearCol));

            matches = FindAllMatches();
        }
        
        if(!CanMakeMatches()) RemoveBoard();
        
        isProcessing = false;
    }

    private IEnumerator CoSwaping((int x, int y) a, (int x, int y) b)
    {
        var gemA = _gems[a.x, a.y];
        var gemB = _gems[b.x, b.y];

        _gems[a.x, a.y] = gemB;
        _gems[b.x, b.y] = gemA;

        Vector3 startA = gemA.transform.position;
        Vector3 startB = gemB.transform.position;
        
        Vector3 targetA = XYToWorld(a.x, a.y);
        Vector3 targetB = XYToWorld(b.x, b.y);

        float elapsedTime = 0f;
        float duration = 0.2f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            gemA.transform.position = Vector3.Lerp(startA, targetB, t);
            gemB.transform.position = Vector3.Lerp(startB, targetA, t);
            
            yield return null;
        }

        gemA.transform.position = targetB;
        gemB.transform.position = targetA;
    }

    private IEnumerator CoDrop(HashSet<int> clearCol)
    {
        var dropGemList = new List<(Gem gem, Vector3 start, Vector3 end)>();

        foreach (var x in clearCol)
        {
            var rows = new List<int>();
            for (int y = 0; y < _height; y++)
            {
                if(HasCell(x, y)) rows.Add(y);
            }
            if (rows.Count == 0) continue;

            int currentY = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int y = rows[i];
                if (!HasGem(x, y)) continue;
                var g = _gems[x, y];

                int targetY = rows[currentY];
                if (y != targetY)
                {
                    _gems[x, y] = null;
                    _gems[x, targetY] = g;
                    dropGemList.Add((g, g.transform.position, XYToWorld(x, targetY)));
                }
                currentY++;
            }

            int empty = rows.Count - currentY;
            if (empty <= 0) continue;

            int topY = GetTopCellY(x);
            for (int k = 0; k < empty; k++)
            {
                int targetY = rows[currentY + k];
                int spawnBoardY = topY + (k + 1);
                Vector3 spawn = XYToWorld(x, spawnBoardY);
                Vector3 target = XYToWorld(x, targetY);

                var gem = gemPool.Get();
                gem.transform.position = spawn;
                gem.transform.rotation = Quaternion.identity;

                AvoidImmediateMatch(gem, x, targetY);
                _gems[x, targetY] = gem;
                
                dropGemList.Add((gem, spawn, target));
            }
        }

        yield return StartCoroutine(CoDroping(dropGemList));
    }

    private IEnumerator CoDroping(List<(Gem gem, Vector3 start, Vector3 end)> dropGemList)
    {
        if (dropGemList.Count > 0)
        {
            float dropSpeed = 5f;

            var dists = new List<float>(dropGemList.Count);
            float maxTime = 0f;
            for (int i = 0; i < dropGemList.Count; i++)
            {
                float dist = Vector3.Distance(dropGemList[i].start, dropGemList[i].end);
                dists.Add(dist);
                float time = (dist <= 0f) ? 0f : dist / dropSpeed;
                if (time > maxTime) maxTime = time;
            }

            float elapsed = 0f;
            while (elapsed < maxTime)
            {
                elapsed += Time.deltaTime;

                for (int i = 0; i < dropGemList.Count; i++)
                {
                    var dropGem = dropGemList[i];
                    float dist = dists[i];

                    if (dist <= 0f)
                    {
                        dropGem.gem.transform.position = dropGem.end;
                        continue;
                    }

                    var cur = dropGem.gem.transform.position;
                    dropGem.gem.transform.position = Vector3.MoveTowards(cur, dropGem.end, dropSpeed * Time.deltaTime);
                }

                yield return null;
            }
        }
    }
}
