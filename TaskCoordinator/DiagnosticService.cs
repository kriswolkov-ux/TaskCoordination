using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TaskCoordinator;

public sealed class DiagnosticService
{
    public async Task<DiagnosticResult> RunAsync(string name, TimeSpan duration, CancellationToken token = default)
    {
        // 1 Проверить имя и неотрицательную длительность.
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя диагностики не может быть пустым.", nameof(name));

        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Длительность не может быть отрицательной.");

        // 2 Передать token в Task.Delay.
        await Task.Delay(duration, token);

        // 3 Вернуть статус Completed и длительность.
        return new DiagnosticResult(name, DiagnosticStatus.Completed, "Успешно", duration);
    }

    public async Task<DiagnosticResult> RunCpuBoundAsync(string name, int iterations, CancellationToken token = default)
    {
        // 4 Перенести CPU-bound цикл в Task.Run; регулярно проверять token.
        return await Task.Run(() =>
        {
            long sum = 0;
            for (int i = 0; i < iterations; i++)
            {
                if (i % 1000 == 0)
                    token.ThrowIfCancellationRequested();
                sum += i % 7;
            }
            return new DiagnosticResult(name, DiagnosticStatus.Completed, $"Сумма: {sum}", TimeSpan.Zero);
        }, token);
    }

    public async Task<IReadOnlyList<DiagnosticResult>> RunAllAsync(CancellationToken token = default)
    {
        var tasks = new[]
        {
            RunAsync("Сеть", TimeSpan.FromMilliseconds(600), token),
            RunAsync("Диск", TimeSpan.FromMilliseconds(900), token),
            RunAsync("База", TimeSpan.FromMilliseconds(700), token)
        };

        // 5 Дождаться всех задач через WhenAll.
        return await Task.WhenAll(tasks);
    }

    public async Task<DiagnosticResult> RunWithTimeoutAsync(string name, TimeSpan duration, TimeSpan timeout, CancellationToken token = default)
    {
        // 6 Создать linked CancellationTokenSource на основе внешнего token.
        // 7 Настроить CancelAfter(timeout).
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        linkedCts.CancelAfter(timeout);

        try
        {
            return await RunAsync(name, duration, linkedCts.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // 8 Отличить тайм-аут от отмены вызывающей стороны; тайм-аут сообщать TimeoutException.
            throw new TimeoutException($"Диагностика '{name}' превысила время выполнения {timeout}");
        }
    }

    public async Task<string> FirstFinishedAsync(CancellationToken token = default)
    {
        var tasks = new[]
        {
            RunAsync("Быстрая", TimeSpan.FromMilliseconds(150), token),
            RunAsync("Средняя", TimeSpan.FromMilliseconds(350), token),
            RunAsync("Медленная", TimeSpan.FromMilliseconds(600), token)
        };

        // 9 Использовать WhenAny, затем await первой задачи.
        var firstTask = await Task.WhenAny(tasks);
        var result = await firstTask;
        return result.Name;
    }

    // 10 Добавить обёртку, которая возвращает статусы Failed/Cancelled
    // и текст ошибки вместо сокрытия исключений.
    public async Task<DiagnosticResult> RunSafeAsync(string name, TimeSpan duration, CancellationToken token = default)
    {
        try
        {
            return await RunAsync(name, duration, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new DiagnosticResult(name, DiagnosticStatus.Cancelled, "Отменено пользователем", TimeSpan.Zero);
        }
        catch (OperationCanceledException)
        {
            return new DiagnosticResult(name, DiagnosticStatus.Cancelled, "Отменено по тайм-ауту", TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            return new DiagnosticResult(name, DiagnosticStatus.Failed, $"Ошибка: {ex.Message}", TimeSpan.Zero);
        }
    }
}