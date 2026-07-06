using System;

namespace ShipSalvage.AI
{
    public enum BTStatus
    {
        Success,
        Running,
        Failure
    }

    public abstract class BTNode
    {
        public abstract BTStatus Tick();
    }

    public class Selector : BTNode
    {
        private readonly BTNode[] _children;

        public Selector(params BTNode[] children) => _children = children;

        public override BTStatus Tick()
        {
            foreach (var child in _children)
            {
                var status = child.Tick();
                if (status != BTStatus.Failure)
                    return status;
            }

            return BTStatus.Failure;
        }
    }

    public class Sequence : BTNode
    {
        private readonly BTNode[] _children;

        public Sequence(params BTNode[] children) => _children = children;

        public override BTStatus Tick()
        {
            foreach (var child in _children)
            {
                var status = child.Tick();
                if (status != BTStatus.Success)
                    return status;
            }

            return BTStatus.Success;
        }
    }

    public class ActionNode : BTNode
    {
        private readonly Func<BTStatus> _action;

        public ActionNode(Func<BTStatus> action) => _action = action;

        public override BTStatus Tick() => _action();
    }

    public class ConditionNode : BTNode
    {
        private readonly Func<bool> _condition;

        public ConditionNode(Func<bool> condition) => _condition = condition;

        public override BTStatus Tick() => _condition() ? BTStatus.Success : BTStatus.Failure;
    }
}
