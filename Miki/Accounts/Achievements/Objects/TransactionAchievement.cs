﻿using Miki.Models;
using System;
using System.Threading.Tasks;

namespace Miki.Accounts.Achievements.Objects
{
    internal class TransactionAchievement : BaseAchievement
    {
        public Func<TransactionPacket, Task<bool>> CheckTransaction;

        public override async Task<bool> CheckAsync(MikiContext context, BasePacket packet)
        {
            return await CheckTransaction(packet as TransactionPacket);
        }
    }
}