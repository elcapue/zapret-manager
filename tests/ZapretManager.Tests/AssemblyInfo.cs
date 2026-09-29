using Xunit;

// WinForms-тесты создают формы и NotifyIcon: параллельный запуск коллекций на MTA-потоках
// даёт flaky-поведение и взаимовлияние через общий message pump.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
