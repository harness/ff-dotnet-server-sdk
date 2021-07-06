using System;
using System.Collections.Generic;
using System.Text;

namespace io.harness.cfsdk.client.dto
{
    public class Target
    {
        private string identifier;
        private string name;
        private Dictionary<string, string> attributes = new Dictionary<string, string>();
        private bool isPrivate; // If the target is private
        private HashSet<string> privateAttributes = new HashSet<string>(); // Custom set to set the attributes which are private

        public static TargetBuilder builder()
        {
            return new TargetBuilder();
        }

        public Target()
        {
            attributes = new Dictionary<string, string>();
        }

        public Target(string identifier, string name, Dictionary<string, string> attributes, bool isPrivate, HashSet<string> privateAttributes)
        {
            if (attributes == null)
            {
                Attributes = new Dictionary<string, string>();
            }
            else
            {
                Attributes = attributes;
            }

            Identifier = identifier;
            Name = name;
            IsPrivate = isPrivate;
            PrivateAttributes = privateAttributes;
        }

        public string Name { get => name; set => name = value; }
        public string Identifier { get => identifier; set => identifier = value; }
        public Dictionary<string, string> Attributes { get => attributes; set => attributes = value; }
        public bool IsPrivate { get => isPrivate; set => isPrivate = value; }
        public HashSet<string> PrivateAttributes { get => privateAttributes; set => privateAttributes = value; }

        public override string ToString()
        {
            return "TargetId: " + identifier;
        }

        public bool isValid()
        {
            return !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(identifier);
        }
    }

    public class TargetBuilder
    {
        private string identifier;
        private string name;
        private Dictionary<string, string> attributes = new Dictionary<string, string>();
        private bool isPrivate; // If the target is private
        private HashSet<string> privateAttributes; // Custom set to set the attributes which are private

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
