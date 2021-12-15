using System.Collections.Generic;
using System.Threading;
using Marshal = System.Runtime.InteropServices.Marshal;
using RtMidiDll = RtMidi.Unmanaged;

namespace Minis
{
    //
    // MIDI probe class used for enumerating MIDI ports
    //
    // This is actually an RtMidi input object without any input functionality.
    //
    unsafe sealed class MidiProbe : System.IDisposable
    {
        Thread _thread;
        bool _runThread;
        RtMidiDll.Wrapper* _rtmidi;
        static int _portCount = 0;
        static List<string> _portNames = new List<string>();
        int _localPortCount = 0;
        List<string> _localPortNames = new List<string>();

        public MidiProbe()
        {
            _rtmidi = RtMidiDll.InCreateDefault();

            if (_rtmidi == null || !_rtmidi->ok)
            {
                UnityEngine.Debug.LogWarning("Failed to create an RtMidi device object.");
                return;
            }

            _runThread = true;
            _thread = new Thread(() =>
            {
                while(_runThread)
                {
                    var portCount = (int)RtMidiDll.GetPortCount(_rtmidi);
                    var portNames = new List<string>();
                    for (int n = 0; n < portCount; n++)
                    {
                        portNames.Add(Marshal.PtrToStringAnsi(RtMidiDll.GetPortName(_rtmidi, (uint)n)));
                    }

                    Monitor.Enter(_portNames);
                    try
                    {
                        _portCount = portCount;
                        _portNames = portNames;
                    }
                    finally
                    {
                        Monitor.Exit(_portNames);
                    }

                    Thread.Sleep(1000 / 30);
                }
            });
            _thread.Start();
        }

        ~MidiProbe()
        {
            if (_thread != null)
            {
                _runThread = false;
                _thread.Join();
                _thread = null;
            }

            if (_rtmidi == null || !_rtmidi->ok) return;
            RtMidiDll.InFree(_rtmidi);
        }

        public void Dispose()
        {
            if (_rtmidi == null || !_rtmidi->ok) return;

            RtMidiDll.InFree(_rtmidi);
            _rtmidi = null;

            System.GC.SuppressFinalize(this);
        }

        public int PortCount {
            get {
                if (Monitor.TryEnter(_portNames))
                {
                    try
                    {
                        _localPortCount = _portCount;
                        _localPortNames = _portNames;
                    }
                    finally
                    {
                        Monitor.Exit(_portNames);
                    }
                }

                return _localPortCount;
            }
        }

        public string GetPortName(int portNumber)
        {
            if (_rtmidi == null || !_rtmidi->ok) return null;

            return portNumber >= _localPortNames.Count ? null : _localPortNames[portNumber];
        }
    }
}
