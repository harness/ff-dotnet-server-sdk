using System;
using System.Collections.Generic;

namespace io.harness.cfsdk.client.dto
{
    public class Target
    {
        public static TargetBuilder builder()
        {
            return new TargetBuilder();
        }

        public Target()
        {
            Attributes = new Dictionary<string, string>();
            PrivateAttributes = new HashSet<string>();
        }

        public Target(string identifier, string name, Dictionary<string, string> attributes, bool isPrivate, HashSet<string> privateAttributes)
        {
            Attributes = attributes ?? new Dictionary<string, string>();
            Identifier = identifier;
            Name = name;
            IsPrivate = isPrivate;
            PrivateAttributes = privateAttributes ?? new HashSet<string>();
        }

        public string Name { get; set; }
        public string Identifier { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
        public bool IsPrivate { get; set; }
        public HashSet<string> PrivateAttributes { get; set; }

        public override string ToString()
        {
            return "TargetId: " + Identifier;
        }

        public bool isValid()
        {
            return !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Identifier);
        }
    }

    public class TargetBuilder
    {
        private string identifier;
        private string name;
        private Dictionary<string, string> attributes = new Dictionary<string, string>();
        private bool isPrivate; // If the target is private
        private HashSet<string> privateAttributes = new HashSet<string>(); // Custom set to set the attributes which are private

        public TargetBuilder()
        {
            attributes = new Dictionary<string, string>();
        }

        public TargetBuilder Identifier(string identifier)
        {
            this.identifier = identifier;
            return this;
        }
        public TargetBuilder Name(string name)
        {
            this.name = name;
            return this;
        }
        public TargetBuilder Attributes(Dictionary<string, string> attributes)
        {
            this.attributes = attributes;
            return this;
        }
        public TargetBuilder IsPrivate(bool isPrivate)
        {
            this.isPrivate = isPrivate;
            return this;
        }
        public TargetBuilder PrivateAttributes(HashSet<string> privateAttributes)
        {
            this.privateAttributes = privateAttributes;
            return this;
        }

        public Target build()
        {
            return new Target(identifier, name, attributes, isPrivate, privateAttributes);
        }
    }
}
