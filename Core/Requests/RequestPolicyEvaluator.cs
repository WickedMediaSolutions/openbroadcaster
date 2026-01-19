using System;
using System.Collections.Generic;
using System.Linq;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Requests
{
    public sealed class RequestPolicyEvaluator
    {
        public bool TryValidate(RequestSettings? settings, IReadOnlyList<QueueItem>? queueSnapshot, string userName, out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (settings == null)
            {
                return true;
            }

            var snapshot = queueSnapshot ?? Array.Empty<QueueItem>();
            if (settings.MaxPendingRequests > 0)
            {
                var pending = snapshot.Count(item => item?.HasRequester == true);
                if (pending >= settings.MaxPendingRequests)
                {
                    rejectionReason = $"request queue full ({pending}/{settings.MaxPendingRequests}).";
                    return false;
                }
            }

            if (settings.MaxRequestsPerUser > 0 && !string.IsNullOrWhiteSpace(userName))
            {
                var perUser = snapshot.Count(item => item != null && string.Equals(item.RequestedBy, userName, StringComparison.OrdinalIgnoreCase));
                if (perUser >= settings.MaxRequestsPerUser)
                {
                    rejectionReason = $"you already have {perUser} pending requests.";
                    return false;
                }
            }

            return true;
        }
    }
}
