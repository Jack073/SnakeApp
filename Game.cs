using System.Text;

namespace SnakeApp;

internal class QueueHandler
{
    private int _currentQueue;

    private readonly (int, int)[][] _queues;
    private readonly int[] _readPtrs;
    private readonly int[] _endPtrs;

    public QueueHandler(int queueSize)
    {
        _queues =
        [
            new (int, int)[queueSize],
            new (int, int)[queueSize]
        ];

        queueSize--;

        _readPtrs = [queueSize, queueSize];
        _endPtrs = [queueSize, queueSize];
        _currentQueue = 0;
    }

    public void Enqueue((int, int) item)
    {
        var queue = 1 - _currentQueue;
        _queues[queue][_endPtrs[queue]--] = item;
    }

    public (int, int)? Next()
    {
        if (_readPtrs[_currentQueue] <= _endPtrs[_currentQueue])
        {
            _readPtrs[_currentQueue] = _queues[_currentQueue].Length - 1;
            _endPtrs[_currentQueue] = _readPtrs[_currentQueue];

            _currentQueue = 1 - _currentQueue;
            if (_readPtrs[_currentQueue] <= _endPtrs[_currentQueue])
                return null;
        }

        return _queues[_currentQueue][_readPtrs[_currentQueue]--];
    }
}

public enum SquareType
{
    Head,
    Body,
    Empty,
    Apple
}

public enum Status
{
    Loss,
    Continue,
    Win
}

public class Game
{
    // Constant, the side length of the snake board. results in a board of _boardSize * _boardSize.
    private readonly int _boardSize;

    // The array making up the game board, each element representing a square.
    private readonly SquareType[,] _board;

    // For body segments, this stores the unicode arrow pointing in the direction of movement for each body segment.
    // This will likely contain more data than there are active body parts, but all active body parts will have
    // their arrows updated as appropriate.
    private readonly char[,] _snakeDirections;

    // THe amount of space left on the board to fill. Set at the beginning as
    // (_boardSize * _boardSize) - snake length - num apples. Used to determine win condition and game end.
    private int _remainingGrowthPotential;

    // The "Queue" implementation responsible for moving body segments. Segments are fed into this and retrieved in
    // such a way that all other segments will be accessed by order of insertion before a segment is revisited.
    // Allows for insertion at any point. 
    private readonly QueueHandler _bodyParts;

    // The location of the snake's head.
    private (int, int) _head;

    // Maintains a list of all spawnable positions for an apple.
    // No inherent ordering to this array, indexing is done by using the _applePositionsMap array.
    private readonly (int, int)[] _possibleApplePositions;

    // The index array for mapping the _possibleApplePositions array.
    // (row, col) input is passed to ConvertToApplePositionMapCoords(row, col). The resulting int is used as the index
    // for this array. If the value at that index is -1, then that coordinate is not a spawnable space for apples.
    // Otherwise, it will return the index of the coordinate in _possibleApplePositions.
    private readonly int[] _applePositionsMap;

    // The number of remaining viable spawn spots in _possibleApplePositions - different
    // to _possibleApplePositions.Length
    private int _applePositionCounter;


    // Self-explanatory Random generator, saved as a class field for reuse.
    private readonly Random _random;

    // Reusable StringBuilder moved to be a class field instead of a local variable to prevent reallocation when a new 
    // text representation is needed.
    private readonly StringBuilder _sb;

    private readonly char[] _parseableBoard;

    public Game(int boardSize, int startingBodySize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(boardSize, 5);

        _board = new SquareType[boardSize, boardSize];
        _boardSize = boardSize;

        _bodyParts = new QueueHandler(boardSize * boardSize);
        _snakeDirections = new char[_boardSize, _boardSize];
        {
            var middleOfBoard = (boardSize - 1) / 2;
            _head = (middleOfBoard, middleOfBoard);
            ArgumentOutOfRangeException.ThrowIfLessThan(middleOfBoard, startingBodySize);

            for (var s = startingBodySize; s > 0; s--)
            {
                _bodyParts.Enqueue((middleOfBoard, middleOfBoard - s));
                _snakeDirections[middleOfBoard, middleOfBoard - s] = Constants.RightArrow;
            }
        }

        _possibleApplePositions = new (int, int)[_boardSize * _boardSize];
        _applePositionCounter = 0;
        _applePositionsMap = new int[_boardSize * _boardSize];

        _sb = new StringBuilder();
        _random = new Random();
        _parseableBoard = new char[1 + (boardSize * boardSize)];
        InitBoard(startingBodySize);
    }

    private void InitBoard(int startingBodySize)
    {
        for (var i = 0; i < _boardSize; i++)
        {
            for (var j = 0; j < _boardSize; j++)
                _board[i, j] = SquareType.Empty;
        }

        _board[_head.Item1, _head.Item2] = SquareType.Head;

        for (var i = 0; i < startingBodySize; i++)
        {
            var coord = _bodyParts.Next();
            if (!coord.HasValue)
                break;
            _board[coord.Value.Item1, coord.Value.Item2] = SquareType.Body;
            _bodyParts.Enqueue(coord.Value);
        }

        for (var i = 0; i < _applePositionsMap.Length; i++)
        {
            _applePositionsMap[i] = -1;
        }

        for (var x = 0; x < _boardSize; x++)
        {
            for (var y = 0; y < _boardSize; y++)
            {
                if (_board[x, y] != SquareType.Empty) continue;
                _possibleApplePositions[_applePositionCounter] = (x, y);
                _applePositionsMap[(_boardSize * x) + y] = _applePositionCounter++;
            }
        }

        var totalIncrementer = _applePositionCounter;
        for (var x = 0; x < _boardSize; x++)
        {
            for (var y = 0; y < _boardSize; y++)
            {
                if (_board[x, y] == SquareType.Empty) continue;
                _possibleApplePositions[totalIncrementer] = (x, y);
                _applePositionsMap[(_boardSize * x) + y] = totalIncrementer++;
            }
        }

        _remainingGrowthPotential = ((_boardSize * _boardSize) - startingBodySize) - 1;

        NewApple();

        _sb.EnsureCapacity(_boardSize * ((
            MaxInt(
                AppleColour.Length,
                HeadColour.Length,
                BodyColour.Length,
                EmptyColour.Length,
                EndFormatting.Length
            ) * _boardSize) + 1));
    }

    private static int MaxInt(int firstN, params int[] n)
    {
        return n.Prepend(firstN).Max();
    }

    private int ConvertToApplePositionMapCoords(int x, int y)
    {
        return (_boardSize * x) + y;
    }

    private void SwapPotentialApples((int, int) old, (int, int) newSpace)
    {
        var oldConvertedCoords = ConvertToApplePositionMapCoords(old.Item1, old.Item2);
        var newConvertedCoords = ConvertToApplePositionMapCoords(newSpace.Item1, newSpace.Item2);

        var oldPotentialAppleIndex = _applePositionsMap[oldConvertedCoords];
        var newPotentialAppleIndex = _applePositionsMap[newConvertedCoords];

        _applePositionsMap[newConvertedCoords] = oldPotentialAppleIndex;
        _applePositionsMap[oldConvertedCoords] = newPotentialAppleIndex;

        _possibleApplePositions[oldPotentialAppleIndex] = newSpace;
    }

    private void NewApple()
    {
        // _possibleApplePositions is an array containing all the spawnable places for apples, arranged such
        // that the first _applePositionCounter items are valid spawn locations. This will select one of those locations
        // randomly.
        var appleIndex = _random.Next(_applePositionCounter);
        var appleCoords = _possibleApplePositions[appleIndex];

        var oldEndSpawnPos = _possibleApplePositions[--_applePositionCounter];
        _possibleApplePositions[_applePositionCounter] = appleCoords;
        _possibleApplePositions[appleIndex] = oldEndSpawnPos;

        _applePositionsMap[ConvertToApplePositionMapCoords(appleCoords.Item1, appleCoords.Item2)] = _applePositionCounter;
        _applePositionsMap[ConvertToApplePositionMapCoords(oldEndSpawnPos.Item1, oldEndSpawnPos.Item2)] = appleIndex;

        _board[appleCoords.Item1, appleCoords.Item2] = SquareType.Apple;
    }

    private void ConsumeApple(int appleX, int appleY)
    {
        _board[appleX, appleY] = SquareType.Empty;
    }

    private const string EndFormatting = "\e[0m";

    private const string AppleColour = "\e[37;101m";
    private const string HeadColour = "\e[37;102m";
    private const string BodyColour = "\e[37;103m";
    private const string EmptyColour = "\e[34;7m";

    private string GetBoardOutput()
    {
        _sb.Length = 0;

        SquareType? previousSquare = null;

        for (var x = 0; x < _boardSize; x++)
        {
            for (var y = 0; y < _boardSize; y++)
            {
                var currentSquare = _board[x, y];

                if (currentSquare != previousSquare)
                {
                    if (previousSquare != null)
                        _sb.Append(EndFormatting);

                    switch (currentSquare)
                    {
                        case SquareType.Head:
                            _sb.Append(HeadColour);
                            break;
                        case SquareType.Body:
                            _sb.Append(BodyColour);
                            break;
                        case SquareType.Apple:
                            _sb.Append(AppleColour);
                            break;
                        case SquareType.Empty:
                            _sb.Append(EmptyColour);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                switch (currentSquare)
                {
                    case SquareType.Head:
                        _sb.Append('H');
                        break;
                    case SquareType.Body:
                        _sb.Append(_snakeDirections[x, y]);
                        break;
                    case SquareType.Apple:
                        _sb.Append('A');
                        break;
                    case SquareType.Empty:
                        _sb.Append('E');
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                previousSquare = currentSquare;
            }

            _sb.Append('\n');
        }

        _sb.Append(EndFormatting);

        return _sb.ToString();
    }

    public char[] GetParseableBoardState()
    {
        var pos = 0;

        for (var x = 0; x < _boardSize; x++)
        {
            for (var y = 0; y < _boardSize; y++)
            {
                var currentSquare = _board[x, y];

                _parseableBoard[pos++] = currentSquare switch
                {
                    SquareType.Head => 'H',
                    SquareType.Body => _snakeDirections[x, y],
                    SquareType.Apple => 'A',
                    SquareType.Empty => 'X',
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        return _parseableBoard;
    }

    public void PrintBoard()
    {
        Console.Write(GetBoardOutput());
    }

    private static (int, int) GetDirectionDelta(Direction direction)
    {
        return direction switch
        {
            Direction.Up => (-1, 0),
            Direction.Down => (1, 0),
            Direction.Left => (0, -1),
            Direction.Right => (0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }

    private static char ArrowFromDirection(Direction direction)
    {
        return direction switch
        {
            Direction.Up => Constants.UpArrow,
            Direction.Down => Constants.DownArrow,
            Direction.Left => Constants.LeftArrow,
            Direction.Right => Constants.RightArrow,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }

    public Status Tick(Direction direction)
    {
        var directionDelta = GetDirectionDelta(direction);
        var oldHeadX = _head.Item1;
        var oldHeadY = _head.Item2;

        var newX = _head.Item1 + directionDelta.Item1;
        var newY = _head.Item2 + directionDelta.Item2;

        if (newX < 0 || newX >= _boardSize || newY < 0 || newY >= _boardSize)
        {
            return Status.Loss;
        }

        var occupyingSquare = _board[newX, newY];

        switch (occupyingSquare)
        {
            case SquareType.Body:
                return Status.Loss;
            case SquareType.Apple:
                ConsumeApple(newX, newY);

                _board[newX, newY] = SquareType.Head;
                _head = (newX, newY);
                _board[oldHeadX, oldHeadY] = SquareType.Body;
                _bodyParts.Enqueue((oldHeadX, oldHeadY));

                _snakeDirections[oldHeadX, oldHeadY] = ArrowFromDirection(direction);

                _remainingGrowthPotential--;
                if (_remainingGrowthPotential == 0)
                {
                    return Status.Win;
                }

                NewApple();

                break;
            case SquareType.Empty:
                var nextSegment = _bodyParts.Next();
                if (!nextSegment.HasValue)
                {
                    return Status.Win;
                }

                _board[nextSegment.Value.Item1, nextSegment.Value.Item2] = SquareType.Empty;
                _bodyParts.Enqueue((oldHeadX, oldHeadY));
                _board[oldHeadX, oldHeadY] = SquareType.Body;
                _snakeDirections[oldHeadX, oldHeadY] = ArrowFromDirection(direction);

                _board[newX, newY] = SquareType.Head;
                _head = (newX, newY);

                SwapPotentialApples((newX, newY), nextSegment.Value);
                break;
            case SquareType.Head:
                break;
            default:
                throw new ArgumentOutOfRangeException(occupyingSquare.ToString());
        }

        return Status.Continue;
    }
}