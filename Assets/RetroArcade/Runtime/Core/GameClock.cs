using System;
using System.Collections.Generic;

namespace RetroArcade
{
    /// <summary>
    /// Equivalente di <c>this.time</c> in Phaser: orologio della scena (in millisecondi)
    /// con eventi ciclici (<c>time.addEvent({ loop: true })</c>) e chiamate ritardate
    /// (<c>time.delayedCall</c>). Ogni scena ha il suo clock e viene buttato via quando
    /// la scena termina, quindi nessun timer "orfano" sopravvive al cambio di scena.
    /// </summary>
    public sealed class GameClock
    {
        private readonly List<TimerEvent> _events = new List<TimerEvent>();
        private readonly List<TimerEvent> _snapshot = new List<TimerEvent>();

        /// <summary>Millisecondi trascorsi da quando la scena è partita (come <c>this.time.now</c>).</summary>
        public double Now { get; private set; }

        /// <summary>Evento ciclico: richiama <paramref name="callback"/> ogni <paramref name="delayMs"/> ms.</summary>
        public TimerEvent AddLoop(float delayMs, Action callback)
        {
            var e = new TimerEvent(delayMs, true, callback);
            _events.Add(e);
            return e;
        }

        /// <summary>Chiamata singola dopo <paramref name="delayMs"/> ms.</summary>
        public TimerEvent DelayedCall(float delayMs, Action callback)
        {
            var e = new TimerEvent(delayMs, false, callback);
            _events.Add(e);
            return e;
        }

        public void Clear() => _events.Clear();

        internal void Tick(float deltaMs)
        {
            Now += deltaMs;
            _snapshot.Clear();
            _snapshot.AddRange(_events);

            foreach (var e in _snapshot)
            {
                if (e.Removed) continue;
                e.Elapsed += deltaMs;
                if (e.Elapsed < e.Delay) continue;

                // Come Phaser: al massimo uno scatto per frame, il resto viene conservato
                // (ma limitato a un intero periodo, così un frame lento non crea raffiche).
                float remainder = e.Elapsed - e.Delay;
                e.Elapsed = Math.Min(remainder, e.Delay);
                if (!e.Loop) e.Removed = true;
                e.Callback?.Invoke();
            }

            _events.RemoveAll(ev => ev.Removed);
        }
    }

    public sealed class TimerEvent
    {
        /// <summary>Periodo in millisecondi. Si può cambiare al volo (come <c>timer.delay = x</c> in Phaser).</summary>
        public float Delay;
        public readonly bool Loop;
        internal readonly Action Callback;
        internal float Elapsed;
        internal bool Removed;

        internal TimerEvent(float delay, bool loop, Action callback)
        {
            Delay = Math.Max(1f, delay);
            Loop = loop;
            Callback = callback;
        }

        public void Remove() => Removed = true;
    }
}
