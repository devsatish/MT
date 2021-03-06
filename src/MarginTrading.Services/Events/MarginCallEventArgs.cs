using System;
using MarginTrading.Core;

namespace MarginTrading.Services.Events
{
    public class MarginCallEventArgs
    {
        public MarginCallEventArgs(MarginTradingAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            Account = account;
        }

        public MarginTradingAccount Account { get; set; }
    }
}