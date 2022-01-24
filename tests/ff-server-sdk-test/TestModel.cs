using System;
using System.Collections.Generic;
using io.harness.cfsdk.HarnessOpenAPIService;

namespace ff_server_sdk_test
{


    public class TestModel
    {
        public string testFile;
        public FeatureConfig flag;
        public List<io.harness.cfsdk.client.dto.Target> targets;
        public List<Segment> segments;
        public Dictionary<string, bool> expected;
        public TestModel()
        {
        }
    }
}
