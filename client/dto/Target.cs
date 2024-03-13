using System;
using System.Collections.Generic;

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
        
        public Target(string identifier, string name, Dictionary<string, string> attributes)
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
            IsPrivate = false;
        }
        
        public Target(string identifier, string name, Dictionary<string, string> attributes, bool isPrivate)
        {
            this.attributes = attributes ?? new Dictionary<string, string>();
            this.identifier = identifier;
            this.name = name;
            this.isPrivate = isPrivate;
        }


        [Obsolete("privateAttributes will be removed in a future release. Use Target(string identifier, string name, Dictionary<string, string> attributes, bool isPrivate) to mark the entire target as private.")]
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

        [Obsolete("Private attributes will be removed in a future release")]
        public HashSet<string> PrivateAttributes { get => privateAttributes; set => privateAttributes = value; }

        public override string ToString()
        {
            return "TargetId: " + identifier;
        }

        public bool isValid()
        {
            return !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(identifier);
        }
        
        
        public override bool Equals(object obj)
        {
            if (obj is Target other)
            {
                return Identifier == other.Identifier && AreDictionariesEqual(attributes, other.attributes);
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            // Overflow is fine, just wrap
            unchecked 
            {
                int hash = 17;

                // Hash code for Identifier
                hash = hash * 31 + (Identifier != null ? Identifier.GetHashCode() : 0);

                // Combine hash codes for each key-value pair in the dictionary
                foreach (var pair in attributes)
                {
                    hash = hash * 31 + (pair.Key != null ? pair.Key.GetHashCode() : 0);
                    hash = hash * 31 + (pair.Value != null ? pair.Value.GetHashCode() : 0);
                }

                return hash;
            }
        }

        private static bool AreDictionariesEqual(Dictionary<string, string> dict1, Dictionary<string, string> dict2)
        {
            if (dict1.Count != dict2.Count) 
                return false;

            foreach (var pair in dict1)
            {
                if (!dict2.TryGetValue(pair.Key, out var value) || value != pair.Value)
                    return false;
            }
            return true;
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

        [Obsolete("Private attributes will be removed in a future release")]
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
