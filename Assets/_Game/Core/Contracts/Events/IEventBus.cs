using System;

namespace SeasonalBastion.Contracts
{
    public interface IEventBus
    {
        void Publish<T>(T evt) where T : struct;
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
    }
}
