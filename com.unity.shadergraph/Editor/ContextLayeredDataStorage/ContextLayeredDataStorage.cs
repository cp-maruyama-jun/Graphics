using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace UnityEditor.ContextLayeredDataStorage
{
    public interface IDataElement
    {
        public bool TryGetData<T>(out T data);
    }

    [Serializable]
    public class ContextLayeredDataStorage : ISerializationCallbackReceiver
    {
        protected class Element : IDataElement
        {
            public string id;
            public List<Element> children;
            public Element parent;
            public ContextLayeredDataStorage owner;
            public string serializedData;

            public Element(ContextLayeredDataStorage owner)
            {
                id = "";
                parent = null;
                children = new List<Element>();
                this.owner = owner;
            }

            public Element(string id, ContextLayeredDataStorage owner) : this(owner)
            {
                this.id = id;
            }

            //It is a path id if there are any '.' characters
            public static bool IsPath(string id)
            {
                return id.IndexOf('.') >= 0;
            }

            public string GetFullPath()
            {
                string output = id;
                Element traverser = parent;
                while(traverser != null)
                {
                    if(traverser.id != null && traverser.id.Length > 0)
                    {
                        output = traverser.id + "." + output;
                    }
                    traverser = traverser.parent;
                }
                return output;
            }

            public void AddChild(Element child)
            {
                child.parent = this;
                children.Add(child);
            }

            public void LinkElement(Element other)
            {
                other.AddChild(this);
                this.AddChild(other);
            }

            public Element<T> AddData<T>(string id, T data)
            {
                //not a path? Add it directly as a child
                if (!IsPath(id))
                {
                    var output = new Element<T>(id, data, owner);
                    AddChild(output);
                    return output;
                }
                else
                {
                    //is a path? see if it matches any children
                    foreach (Element child in children)
                    {
                        if (id.IndexOf(child.id) == 0)
                        {
                            return child.AddData(id.Substring(child.id.Length).TrimStart('.'), data);
                        }
                    }
                    //otherwise, add as child with full name
                    var output = new Element<T>(id, data, owner);
                    AddChild(output);
                    return output;
                }
            }

            public Element AddData(string id)
            {
                //not a path? Add it directly as a child
                if (!IsPath(id))
                {
                    var output = new Element(id, owner);
                    AddChild(output);
                    return output;
                }
                else
                {
                    //is a path? see if it matches any children
                    foreach (Element child in children)
                    {
                        if (id.IndexOf(child.id) == 0)
                        {
                            return child.AddData(id.Substring(child.id.Length).TrimStart('.'));
                        }
                    }
                    //otherwise, add as child with full name
                    var output = new Element(id, owner);
                    AddChild(output);
                    return output;
                }
            }

            public void RemoveChild(Element child)
            {
                child.parent = null;
                children.Remove(child);
            }
            public void Remove()
            {
                foreach (var child in children)
                {
                    child.id = $"{id}.{child.id}";
                    if (parent != null)
                    {
                        RemoveChild(child);
                        parent.AddChild(child);
                        break;

                    }
                }
                if (parent != null)
                {
                    parent.RemoveChild(this);
                }
            }

            public void RemoveWithoutFix()
            {
                if(parent != null)
                {
                    parent.RemoveChild(this);
                }
            }

            public bool TryGetData<T>(out T data)
            {
                var isDataHolder = this as Element<T>;
                if(isDataHolder != null)
                {
                    data = isDataHolder.data;
                    return true;
                }
                else
                {
                    data = default(T);
                    return false;
                }
            }

            public virtual (string, string, string) ToSerializedFormat()
            {
                return (GetFullPath(), null, null);
            }

        }

        protected class Element<T> : Element, IDataElement
        {
            public T data;

            [Serializable]
            public struct DataBox
            {
                public T m_data;
            }
            

            public Element(string id, T data, ContextLayeredDataStorage owner) : base(id,owner)
            {
                this.data = data;
            }


            public override (string, string, string) ToSerializedFormat()
            {
                try
                {
                    return (GetFullPath(), typeof(T).AssemblyQualifiedName, JsonUtility.ToJson(new DataBox() { m_data = data }, true));
                }
                catch
                {
                    Debug.LogError($"Could not serialize data associated with {GetFullPath()}: {data}");
                    return (GetFullPath(), typeof(T).AssemblyQualifiedName, null);
                }
            }

        }

        public struct LayerDescriptor
        {
            public string name;
            public bool isSerialized;
        }

        protected class ReverseIntComparer : IComparer<int>
        {
            //reverse order since all our searching will check highest layer first
            public int Compare(int x, int y) => x.CompareTo(y) * -1;
        }

        private class SerializedDataComparer : IComparer<SerializedElementdData>
        {
            public int Compare(SerializedElementdData x, SerializedElementdData y)
            {
                return x.id.CompareTo(y.id);
            }
        }

        [Serializable]
        public struct SerializedElementdData
        {
            public string id;
            public string type;
            public string data;

            public SerializedElementdData((string id, string type, string data) input)
            {
                this.id = input.id;
                this.type = input.type;
                this.data = input.data;
            }
        }

        [Serializable]
        public struct SerializedLayer
        {
            public string layerId;
            public List<SerializedElementdData> data;
        }

        [NonSerialized]
        protected readonly SortedList<int, (LayerDescriptor descriptor, Element element)> m_layerList;
        public List<SerializedLayer> m_serializedData;

        [NonSerialized]
        protected Element m_flatStructure;
        [NonSerialized]
        protected Dictionary<string, Element> m_flatStructureLookup;
        public ContextLayeredDataStorage()
        {
            m_layerList = new SortedList<int, (LayerDescriptor layerID, Element element)>(new ReverseIntComparer());
            m_flatStructure = new Element(this);
            m_flatStructureLookup = new Dictionary<string, Element>();
            AddDefaultLayers();
        }

        protected virtual void AddDefaultLayers()
        {
            m_layerList.Add(-1, (new LayerDescriptor() { name = "Root" }, new Element<int>("", -1, this)));
        }

        public void AddNewTopLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                throw new System.ArgumentException("Cannor use a null or empty layer name", "layerName");
            }
            m_layerList.Add(m_layerList.Keys[0] + 1, (new LayerDescriptor() { name = layerName }, new Element(this)));
        }

        //Grab the root element of the first layer in layer list with the name == layerID
        protected Element GetLayerRoot(string layerID)
        {
            foreach(var (id, elem) in m_layerList.Values)
            {
                if(layerID.CompareTo(id.name) == 0)
                {
                    return elem;
                }
            }
            return null;
        }

        //AddData with no specified layer gets added to the topmost layer
        protected void AddData<T>(string id, T data, out Element<T> elem)
        {
            AddData(m_layerList[m_layerList.Keys[0]].element, id, data, out elem);
        }

        public IDataElement AddData<T>(string id, T data)
        {
            AddData(id, data, out Element<T> element);
            return element;
        }

        protected void AddData(string id, out Element elem)
        {
            AddData(m_layerList[m_layerList.Keys[0]].element, id, out elem);
        }

        public IDataElement AddData(string id)
        {
            AddData(id, out Element elem);
            return elem;
        }

        protected void AddData<T>(LayerDescriptor layer, string id, T data, out Element<T> elem)
        {
            elem = null;
            foreach(var l in m_layerList)
            {
                if(string.CompareOrdinal(l.Value.descriptor.name, layer.name) == 0)
                {
                    AddData(l.Value.element, id, data, out elem);
                }
            }
        }

        public IDataElement AddData<T>(string layer, string id, T data)
        {
            AddData(new LayerDescriptor() { name = layer }, id, data, out Element<T> elem);
            return elem;
        }

        protected void AddData(LayerDescriptor layer, string id, out Element elem)
        {
            elem = null;
            foreach (var l in m_layerList)
            {
                if (string.CompareOrdinal(l.Value.descriptor.name, layer.name) == 0)
                {
                    AddData(l.Value.element, id, out elem);
                }
            }
        }

        public IDataElement AddData(LayerDescriptor layer, string id)
        {
            AddData(layer, id, out Element elem);
            return elem;
        }

        protected void AddData<T>(Element elem, string id, T data, out Element<T> output)
        {
            output = elem.AddData(id, data);
        }

        protected void AddData(Element elem, string id, out Element output)
        {
            output = elem.AddData(id);
        }

        public void RemoveData(string id)
        {
            if(string.IsNullOrEmpty(id))
            {
                throw new System.ArgumentException("Cannot remove a null or empty string id", "id");
            }
            SearchInternal(id)?.Remove();
        }

        protected void RemoveData(Element elem)
        {
            if(elem.id.Length == 0 && elem.parent == null)
            {
                throw new System.ArgumentException("Cannot remove the root element of a layer", "elem");
            }
            elem.Remove();
        }

        public IDataElement Search(string lookup)
        {
            return SearchInternal(lookup);
        }

        //Search layers in hierarchical order for the first element with the name lookup
        protected Element SearchInternal(string lookup)
        {
            foreach(var layer in m_layerList)
            {
                Element elem = SearchRecurse(layer.Value.element, lookup);
                if(elem != null)
                {
                    return elem;
                }
            }
            return null;
        }

        protected Element SearchRelative(Element elem, string lookup)
        {
            if(elem.children.Count > 0)
            {
                Element output = null;
                foreach(Element child in elem.children)
                {
                    output = SearchRecurse(child, lookup);
                    if(output != null)
                    {
                        return output;
                    }
                }
            }
            return null;
        }

        //may rewrite as non recursive
        protected Element SearchRecurse(Element elem, string lookup)
        {
            if (string.CompareOrdinal(elem.id, lookup) == 0)
            {
                return elem;
            }

            if(lookup.IndexOf(elem.id) == 0 && elem.children.Count > 0)
            {
                Element output = null;
                string nextLookup = lookup.Substring(elem.id.Length).TrimStart('.');
                foreach(Element child in elem.children)
                {
                    output = SearchRecurse(child, nextLookup);
                    if(output != null)
                    {
                        return output;
                    }
                }
            }
            return null;
        }

        protected bool Contains(Element elem)
        {
            return elem.owner == this;
        }

        protected void Rebalance()
        {
            foreach(var layer in m_layerList)
            {
                Rebalance(layer.Value.element, GatherAll(layer.Value.element));
            }
        }

        protected List<Element> GatherAll(Element root)
        {
            List<Element> accumulator = new List<Element>();
            foreach(var child in root.children)
            {
                accumulator.AddRange(GatherAll(child));
                accumulator.Add(child);
            }
            return accumulator;
        }

        protected void Rebalance(Element root, IEnumerable<Element> elementsWithSharedRoot)
        {
            List<(Element potentialRoot, List<Element> potentialShared)> recurseList = new List<(Element potentialRoot, List<Element> potentialShared)>();

            foreach (var element in elementsWithSharedRoot)
            {
                bool found = false;
                int i;
                for (i = 0; i < recurseList.Count; ++i)
                {
                    var (potentialRoot, potentialShared) = recurseList[i];
                    if (potentialRoot.id.StartsWith(element.id) && potentialRoot.id.Substring(element.id.Length).StartsWith("."))
                    {
                        found = true;
                        potentialShared.Add(potentialRoot);
                        recurseList[i] = (element, potentialShared);
                        break;
                    }
                }
                if(found)
                {
                    List<(Element, List<Element>)> colapsed = new List<(Element, List<Element>)>();
                    var (foundRoot, currentList) = recurseList[i];
                    foreach(var (potentialRoot, potentialShared) in recurseList)
                    {
                        if(foundRoot != potentialRoot && potentialRoot.id.StartsWith(foundRoot.id) && potentialRoot.id.Substring(foundRoot.id.Length).StartsWith("."))
                        {
                            currentList.AddRange(potentialShared);
                            currentList.Add(potentialRoot);
                            colapsed.Add((potentialRoot, potentialShared));
                        }
                    }

                    foreach(var c in colapsed)
                    {
                        recurseList.Remove(c);
                    }
                }
                if (!found)
                {
                    recurseList.Add((element, new List<Element>()));
                }
            }

            foreach (var (nextRoot, nextList) in recurseList)
            {
                if(nextRoot.parent != null)
                {
                    nextRoot.parent.RemoveChild(nextRoot);
                }
                root.AddChild(nextRoot);
                foreach(var elem in nextList)
                {
                    elem.id = elem.id.Substring(nextRoot.id.Length + 1);
                }
                Rebalance(nextRoot, nextList);
            }

        }

        public void OnBeforeSerialize()
        {
            m_serializedData = new List<SerializedLayer>();
            foreach(var layer in m_layerList)
            {
                if(layer.Value.descriptor.isSerialized)
                {
                    var serializedLayer = new SerializedLayer(){layerId = layer.Value.descriptor.name, data = new List<SerializedElementdData>() };
                    foreach(var elem in GatherAll(layer.Value.element))
                    {
                        serializedLayer.data.Add(new SerializedElementdData(elem.ToSerializedFormat()));
                    }
                    serializedLayer.data.Sort(new SerializedDataComparer());
                    m_serializedData.Add(serializedLayer);
                }
            }
        }

        public void OnAfterDeserialize()
        {
            foreach(var serializedLayer in m_serializedData)
            {
                var root = GetLayerRoot(serializedLayer.layerId);
                List<Element> elems = new List<Element>();
                foreach (var data in serializedLayer.data)
                {
                    if (data.type != null && data.type.Length > 0)
                    {
                        Type generic = typeof(Element<>);
                        Type holderGeneric = typeof(Element<>.DataBox);
                        try
                        {
                            Type dataType = Type.GetType(data.type);
                            Type constructed = generic.MakeGenericType(dataType);
                            Type holderConstructed = holderGeneric.MakeGenericType(dataType);
                            System.Reflection.FieldInfo saved = holderConstructed.GetField("m_data");
                            var constructor = constructed.GetConstructor(new Type[] {typeof(string), dataType, typeof(ContextLayeredDataStorage) });
                            object value = saved.GetValue(JsonUtility.FromJson(data.data, holderConstructed));
                            Element elem = (Element)constructor.Invoke(new object[] {data.id, Convert.ChangeType(value, dataType), this });
                            elems.Add(elem);
                        }
                        catch
                        {
                            Debug.LogError($"Could not deserialize the data on element {data.id} of type {data.type}");
                        }
                    }
                    else
                    {
                        elems.Add(new Element(data.id, this));
                    }
                }
                Rebalance(root, elems);
            }
        }

        protected void UpdateFlattenedStructure(Element addedElement)
        {
            if(m_flatStructureLookup.TryGetValue(addedElement.GetFullPath(), out Element elem))
            {

            }
        }
    }
}
