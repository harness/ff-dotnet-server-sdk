using io.harness.cfsdk.client.dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace io.harness.cfsdk.client.api.analytics
{
    public interface ICache
    {
        int get(Analytics a);

        IDictionary<Analytics, int> getAll();

        void put(Analytics a, int i);

        void resetCache();

        void printCache();
    }
}
