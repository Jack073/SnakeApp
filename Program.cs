namespace SnakeApp;

public static class Program
{
    private const int BoardSize = 10;

    private const int MoveDelay = 100;

    private const int StartingBodySize = 3;

    private static void Main(string[] args)
    {
        Manual();
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
}