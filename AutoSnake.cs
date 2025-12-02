namespace SnakeApp;

public class AutoSnake
{
    // The current coordinates of the apple.
    private (int, int) _appleCoords;

    // The current coordinates of the head.
    private (int, int) _headCoords;

    private int _snakeSize;

    // We don't need to know the previous direction to determine the next move.
    // The current implementation of the autoplay is able to act statelessly.
    // These fields are all only used to pass information between methods effectively,
    // no data is retained in an instance of this class in consecutive calls to Tick().

    // The current state of the game board.
    // -1 is an empty space.
    // 0 is the snake's head.
    // Any positive integer is a segment of the snakes body, with the number representing the sequential
    // distance from the snake's head.
    private readonly int[,] _board;

    // The size of one side of the board (all boards are assumed to be squares).
    private readonly int _boardSize;

    // Whether the board has even dimensions (affects the route generation to cover every square).
    private readonly bool _evenBoard;

    // The game object being played.
    private readonly Game _game;

    // The board map represented as a [row, col] array, where each value is the location in
    // sequence that board index would be if the longest route of the board (in a quasi-Hamiltonian grid) was taken. 
    private readonly int[,] _internalRoute;

    // The reverse of _internalRoute, allows looking up a grid location by its sequence number (index).
    private readonly (int, int)[] _internalRouteSequenceLookup;

    private readonly Node[] _routeNodes;

    public AutoSnake(Game game, int boardSize)
    {
        _boardSize = boardSize;
        _board = new int[_boardSize, _boardSize];

        _appleCoords = (-1, -1);
        _headCoords = (0, 0);

        _snakeSize = 0;

        _evenBoard = _boardSize % 2 == 0;

        _game = game;

        _internalRoute = new int[_boardSize, _boardSize];

        _internalRouteSequenceLookup = new (int, int)[_boardSize * _boardSize];

        BuildInternalRoute();

        _routeNodes = new Node[_boardSize * _boardSize];

        BuildNodeMap();
    }

    private void BuildInternalRoute()
    {
        var num = 0;
        _internalRoute[0, 0] = num++;
        if (_evenBoard)
        {
            for (var j = 0; j < _boardSize; j++)
            {
                if (j % 2 == 0)
                {
                    // Descent - first index increases
                    for (var k = 1; k < _boardSize; k++)
                        _internalRoute[k, j] = num++;
                }
                else
                {
                    // Ascent - first index decreases
                    for (var k = _boardSize - 1; k > 0; k--)
                        _internalRoute[k, j] = num++;
                }
            }

            for (var j = _boardSize - 1; j > 0; j--)
                _internalRoute[0, j] = num++;
        }
        else
        {
            // TODO: odd route.
        }

        for (var x = 0; x < _boardSize; x++)
        {
            for (var y = 0; y < _boardSize; y++)
            {
                _internalRouteSequenceLookup[_internalRoute[x, y]] = (x, y);
            }
        }
    }

    private void BuildNodeMap()
    {
        for (var i = 0; i < _routeNodes.Length; i++)
        {
            _routeNodes[i] = new Node(i);
        }

        for (var i = 0; i < _routeNodes.Length; i++)
        {
            var node = _routeNodes[i];
            var coords = _internalRouteSequenceLookup[i];
            foreach (var delta in ((int, int)[])[(0, 1), (0, -1), (1, 0), (-1, 0)])
            {
                var x = coords.Item1 + delta.Item1;
                var y = coords.Item2 + delta.Item2;

                if (x < 0 || x >= _boardSize)
                    continue;

                if (y < 0 || y >= _boardSize)
                    continue;

                var newNode = _routeNodes[_internalRoute[x, y]];

                if (node.IsAForwardDestination(newNode, _boardSize))
                {
                    node.AddNeighbor(newNode);
                }
            }

            if (!_evenBoard)
            {
                if (coords == (2, _boardSize - 1))
                {
                    node.AddNeighbor(_routeNodes[_internalRoute[1, _boardSize - 1]]);
                }
            }

            node.SortNeighbors(_boardSize);
        }
    }

    public void PrintRoute()
    {
        for (var x = 0; x < _boardSize; x++)
        {
            for (var y = 0; y < _boardSize; y++)
            {
                Console.Write(_internalRoute[x, y].ToString().PadLeft(3, '0') + " ");
            }

            Console.WriteLine();
        }
    }

    private static Direction DeriveDirectionFromCoord((int, int) head, (int, int) next)
    {
        return DeriveDirectionFromDelta((next.Item1 - head.Item1, next.Item2 - head.Item2));
    }

    private static Direction DeriveDirectionFromDelta((int, int) delta)
    {
        return delta switch
        {
            (0, 1) => Direction.Right,
            (0, -1) => Direction.Left,
            (1, 0) => Direction.Down,
            (-1, 0) => Direction.Up,
            _ => throw new ArgumentOutOfRangeException(nameof(delta), delta, null)
        };
    }

    public Status Tick()
    {
        // From a code deduplication perspective, it would probably make sense to combine these two methods.
        // However as they will contain different logic, even though there will likely be  a large amount of repetition within them,
        // this should be okay to split into two methods - it is a tradeoff between code duplication and code readability.
        return _evenBoard ? TickEven() : TickOdd();
    }

    private Status TickOdd()
    {
        // TODO
        return Status.Loss;
    }

    private Status TickEven()
    {
        var direction = Direction.Right;

        ParseCurrentBoard();

        var node = _routeNodes[_internalRoute[_headCoords.Item1, _headCoords.Item2]];

        var appleSequenceNumber = _internalRoute[_appleCoords.Item1, _appleCoords.Item2];

        foreach (var route in node.GetRoutes())
        {
            // Rejects routes (A->B) where
            // - B is a part of the snake
            // - |B->A| <= snake length
            // - A < apple sequence location < B

            var targetCoord = _internalRouteSequenceLookup[route.GetSequenceNumber()];

            if (_board[targetCoord.Item1, targetCoord.Item2] > 0)
                // Part of the snake's body.
                continue;

            if (_snakeSize < (_boardSize * _boardSize) - 1)
            {
                var returnCost = ((_boardSize * _boardSize) - route.GetSequenceNumber()) + node.GetSequenceNumber();
                if (returnCost <= _snakeSize)
                {
                    // The snake is too big to use this route, this would cause a collision.
                    continue;
                }
            }

            if (node.GetSequenceNumber() < appleSequenceNumber && appleSequenceNumber < route.GetSequenceNumber())
                // This route would skip past the apple.
                continue;

            var skip = false;
            for (var pos = node.GetSequenceNumber(); pos < route.GetSequenceNumber(); pos++)
            {
                // Console.WriteLine("loop " + pos + " " + route.GetSequenceNumber());
                var convertedCoords = _internalRouteSequenceLookup[pos];
                if (_board[convertedCoords.Item1, convertedCoords.Item2] <= 0) continue;
                skip = true;
                break;
            }

            if (skip)
                continue;

            direction = DeriveDirectionFromCoord(_headCoords, _internalRouteSequenceLookup[route.GetSequenceNumber()]);
            break;
        }
        
        return _game.Tick(direction);
    }


    private void ParseCurrentBoard()
    {
        var boardData = _game.GetParseableBoardState();
        var coords = (0, 0);

        _snakeSize = 0;
        foreach (var point in boardData)
        {
            switch (point)
            {
                case 'A':
                    _board[coords.Item1, coords.Item2] = -1;
                    _appleCoords = coords;
                    break;
                case 'X':
                    _board[coords.Item1, coords.Item2] = -1;
                    break;
                case 'H':
                    _board[coords.Item1, coords.Item2] = 0;
                    _headCoords = coords;
                    _snakeSize++;
                    break;
            }

            IncrCoords(ref coords);
        }

        coords = FindPointingAt(boardData, _headCoords);

        var distanceFromHead = 1;
        while (coords.Item1 != -1)
        {
            _snakeSize++;
            _board[coords.Item1, coords.Item2] = distanceFromHead++;
            coords = FindPointingAt(boardData, coords);
        }
    }

    private (int, int) FindPointingAt(char[] src, (int, int) coords)
    {
        var options = new PointingFromCoords[]
        {
            new(Constants.DownArrow, -_boardSize, x => x > 0, _ => true, (-1, 0)),
            new(Constants.UpArrow, _boardSize, x => x < (_boardSize - 1), _ => true, (1, 0)),
            new(Constants.LeftArrow, 1, _ => true, y => y < _boardSize - 1, (0, 1)),
            new(Constants.RightArrow, -1, _ => true, y => y > 0, (0, -1))
        };

        var translatedCoords = (coords.Item1 * _boardSize) + coords.Item2;

        foreach (var opt in options)
        {
            if (!(opt.CheckX(coords.Item1) && opt.CheckY(coords.Item2)))
            {
                continue;
            }

            if (src[translatedCoords + opt.Delta] == opt.Arrow)
                return (coords.Item1 + opt.CoordinateDelta.Item1, coords.Item2 + opt.CoordinateDelta.Item2);
        }

        return (-1, -1);
    }

    private void IncrCoords(ref (int, int) point)
    {
        if (point.Item2 == (_boardSize - 1))
        {
            point.Item2 = 0;
            point.Item1++;
        }
        else
        {
            point.Item2++;
        }
    }
}

internal struct PointingFromCoords(char arrow, int delta, Func<int, bool> checkX, Func<int, bool> checkY, (int, int) coordinateDelta)
{
    public readonly char Arrow = arrow;
    public readonly int Delta = delta;

    public readonly Func<int, bool> CheckX = checkX;
    public readonly Func<int, bool> CheckY = checkY;

    public (int, int) CoordinateDelta = coordinateDelta;
}

internal class Node(int sequenceNumber)
{
    private readonly int _sequenceNumber = sequenceNumber;

    private List<Node> _validNeighbours = [];

    // Determine if n2 is classed as further along than the instance node. 
    public bool IsAForwardDestination(Node n2, int boardSize)
    {
        var maxSequenceNumber = (boardSize * boardSize) - 1;
        if (_sequenceNumber == maxSequenceNumber && n2._sequenceNumber == 0)
            return true;
        if (_sequenceNumber == 0 && n2._sequenceNumber == maxSequenceNumber)
            return false;
        return _sequenceNumber < n2._sequenceNumber;
    }

    // Calculate the distance it would take to move from the instance Node to the Node n.
    private int DistanceToNode(Node n, int boardSize)
    {
        var maxSequenceNumber = (boardSize * boardSize) - 1;
        if (n._sequenceNumber == 0 && _sequenceNumber == maxSequenceNumber)
            return 1;
        return n._sequenceNumber - _sequenceNumber;
    }

    public void AddNeighbor(Node n)
    {
        _validNeighbours.Add(n);
    }

    // Sort the neighbours of this node by priority, in descending order.
    public void SortNeighbors(int boardSize)
    {
        // This will organise the possible routes from this node based on the priority, where a route that progresses
        // further along the board is given higher priority. This enables us to utilise shortcuts where available.
        _validNeighbours = _validNeighbours.OrderByDescending(n => DistanceToNode(n, boardSize)).ToList();
    }

    public int GetSequenceNumber()
    {
        return _sequenceNumber;
    }

    public IReadOnlyList<Node> GetRoutes()
    {
        return _validNeighbours;
    }
}