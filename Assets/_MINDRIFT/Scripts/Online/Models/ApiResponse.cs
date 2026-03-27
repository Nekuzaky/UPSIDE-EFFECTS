using System;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class ApiResponse<TData>
    {
        public bool success;
        public string message;
        public TData data;
    }

    [Serializable]
    public sealed class ApiMessageEnvelope
    {
        public bool success;
        public string message;
    }
}
