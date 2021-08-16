using System.Collections.Generic;
using UnityEditor.ShaderGraph.Registry;

namespace UnityEditor.ShaderGraph.GraphDelta
{
    public interface IGraphHandler
    {
        public INodeWriter AddNode<T>(string name, IRegistry registry) where T : INodeDefinitionBuilder;
        public INodeWriter AddNode(RegistryKey key, string name, IRegistry registry);
        public INodeReader GetNodeReader(string name);
        public INodeWriter GetNodeWriter(string name);
        public void RemoveNode(string name);
        public IEnumerable<INodeReader> GetNodes();

        //public TargetRef AddTarget(TargetType targetType)

        //public void RemoveTarget(TargetRef targetRef)

        //public List<TargetSetting> GetTargetSettings(TargetRef targetRef)

        //public INodeWriter AddNode(NodeType nodeType)

        //public void RemoveNode(INodeRef nodeRef);

        //public NodeType GetNodeType(NodeRef nodeRef)

        //public IEnumerable<INodeReader> GetNodes();

        //public IEnumerable<IPortReader> GetInputPorts(INodeReader nodeRef);

        //public IEnumerable<IPortReader> GetOutputPorts(INodeReader nodeRef);

        //public bool CanConnect(PortRef outputPort, PortRef inputPort)

        //public ConnectionRef Connect(PortRef outputPort, PortRef inputPort)

        //public ConnectionRef ForceConnect(PortRef outputPort, PortRef inputPort)

        //public List<ConnectionRef> GetConnections(PortRef portRef)

        //public void RemoveConnection(ConnectionRef connectionRef)
    }
}
