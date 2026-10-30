using TaskCoordinator;
var pass = 0;
var fail = 0;
void Check(bool ok, string n)
{
    if (ok)
    {
        pass++;
        Console.WriteLine("PASS " + n);
    }
    else
    {
        fail++;
        Console.WriteLine("FAIL " + n);
    }
}
var s = new DiagnosticService();
var one = await s.RunAsync("Сеть", TimeSpan.FromMilliseconds(20));
Check(one.Status == DiagnosticStatus.Completed, "одна диагностика завершается");
var all = await s.RunAllAsync();
Check(
    all.Count == 3 && all.All(x => x.Status == DiagnosticStatus.Completed),
    "WhenAll ждёт три диагностики"
);
var first = await s.FirstFinishedAsync();
Check(first == "Быстрая", "WhenAny возвращает первую завершившуюся");
// Добавить тесты кооперативной отмены, тайм-аута, неверного имени и отрицательного времени.
Console.WriteLine($"Итого: {pass} пройдено, {fail} ошибок.");
return fail == 0 ? 0 : 1;
