namespace SnakeApp;

public static class Program
{
    private const int BoardSize = 10;

    private const int MoveDelay = 100;

    private const int StartingBodySize = 3;

    private static void Main(string[] args)
    {
        if (args is ["auto", ..])
        {
            Autoplay();
        }
        else
        {
            Manual();
        }
    }

    private static void Manual()
    {
        var game = new Game(BoardSize, StartingBodySize);
        var repeat = Status.Continue;

        var direction = Direction.Right;

        while (repeat == Status.Continue)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.LeftArrow:
                        if (direction != Direction.Right)
                            direction = Direction.Left;
                        break;
                    case ConsoleKey.RightArrow:
                        if (direction != Direction.Left)
                            direction = Direction.Right;
                        break;
                    case ConsoleKey.UpArrow:
                        if (direction != Direction.Down)
                            direction = Direction.Up;
                        break;
                    case ConsoleKey.DownArrow:
                        if (direction != Direction.Up)
                            direction = Direction.Down;
                        break;
                    case ConsoleKey.Enter:
                        do
                        {
                            key = Console.ReadKey(true);
                        } while (key.Key != ConsoleKey.Enter);

                        break;
                }
            }

            Console.Clear();

            repeat = game.Tick(direction);
            game.PrintBoard();
            Thread.Sleep(MoveDelay);
        }
    }

    private static void Autoplay()
    {
        var game = new Game(BoardSize, StartingBodySize);
        var solver = new AutoSnake(game, BoardSize);

        var status = Status.Continue;

        game.PrintBoard();
        var moves = 0;

        var timer = new System.Timers.Timer();

        timer.Interval = MoveDelay;
        timer.AutoReset = true;

        var mu = new Mutex();

        timer.Elapsed += (sender, args) =>
        {
            Console.Clear();

            mu.WaitOne();

            moves++;

            status = solver.Tick();
            timer.Enabled = status == Status.Continue;

            // Console.WriteLine("moves: " + moves);

            game.PrintBoard();

            mu.ReleaseMutex();
        };

        timer.Start();

        while (timer.Enabled) ;

        var b = game.GetParseableBoardState();
        var empty = b.Count(move => move == 'X');

        Console.WriteLine($"Remaining: {empty} / {BoardSize * BoardSize} ({100 * empty / (BoardSize * BoardSize)}%)");

        Console.WriteLine("Move count: " + moves);

        Console.WriteLine(status);
    }
}