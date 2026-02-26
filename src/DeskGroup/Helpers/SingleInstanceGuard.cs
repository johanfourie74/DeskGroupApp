using System;
using System.Threading;

namespace DeskGroup.Helpers;

public class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Global\\DeskGroup_SingleInstance_F47AC10B";
    private Mutex? _mutex;
    private bool _ownsIt;

    public bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out _ownsIt);
        return _ownsIt;
    }

    public void Dispose()
    {
        if (_ownsIt && _mutex != null)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _mutex?.Dispose();
    }
}