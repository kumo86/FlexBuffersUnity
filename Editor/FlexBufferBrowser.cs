using System.Collections.Generic;
using System.IO;
using FlexBuffers;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class FlexBufferTreeWindow : EditorWindow
{
    private TreeViewState _treeViewState;

    private FlexBufferTreeView _treeView;
    private string _path = "";
    private string _query = "";
    
    void OnEnable ()
    {
        // Check whether there is already a serialized view state (state 
        // that survived assembly reloading)
        if (_treeViewState == null)
            _treeViewState = new TreeViewState ();
    }
    
    void OnGUI ()
    {
        if (GUILayout.Button("Open FlexBuffer file..."))
        {
            var jsonPath = EditorUtility.OpenFilePanel("Select FlexBuffer file", "", "bytes");
            if (jsonPath.Length == 0)
            {
                return;
            }

            _path = jsonPath;
            
            var bytes = File.ReadAllBytes(_path);

            var root = FlxValue.FromBytes(bytes);
            _treeView = new FlexBufferTreeView(root, _treeViewState);
            _treeViewState = new TreeViewState ();
        }

        if (_path.Length == 0)
        {
            return;
        }
        
        GUILayout.Label(_path);
        var newQuery = GUILayout.TextField(_query);

        if (newQuery != _query)
        {
            var query = FlxQueryParser.Convert(newQuery);
            _query = newQuery;
            _treeView?.SetQuery(query);
        }

        _treeView?.OnGUI(new Rect(0, 80, position.width, position.height - 80));
    }
    
    [MenuItem("Assets/FlexBuffer Browser")]
    static void ShowWidnow()
    {
        var window = GetWindow<FlexBufferTreeWindow> ();
        window.titleContent = new GUIContent ("FlexBuffer Browser");
        window.Show ();
    }
}

public class FlexBufferTreeView : TreeView
{
    private readonly FlxValue _rootValue;
    private FlxQuery _query;

    internal void SetQuery(FlxQuery query)
    {
        _query = query;
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem      { id = -1, depth = -1, displayName = "Root" };
        var flxRoot = new FlxValueTreeViewItem(_rootValue, query:_query);
        root.AddChild(flxRoot);
        return root;
    }
    
    public FlexBufferTreeView(FlxValue rootValue, TreeViewState state) : base(state)
    {
        _rootValue = rootValue;
        Reload();
    }
}

public class FlxValueTreeViewItem : TreeViewItem
{
    private FlxValue _flxValue;
    private int _depth;
    private FlxValueTreeViewItem _parent;
    private string _key;
    private List<TreeViewItem> _children;
    private FlxQuery _query;

    public FlxValueTreeViewItem(FlxValue value, int depth = 0, FlxValueTreeViewItem parent = null, string key = "", FlxQuery query = null)
    {
        _flxValue = value;
        _depth = depth;
        _parent = parent;
        _key = key;
        _query = query;
    }
    
    public override int id => _flxValue.BufferOffset;
    public override string displayName {
        get
        {
            var type = _flxValue.ValueType;
            if (TypesUtil.IsAVector(type))
            {
                return $"{_key}{type}[{_flxValue.AsVector.Length}]";
            }

            if (type == Type.Map)
            {
                return $"{_key}{type}[{_flxValue.AsMap.Length}]";
            }

            if (_flxValue.IsNull)
            {
                return $"{_key}null";
            }

            if (type == Type.Bool)
            {
                return $"{_key}{_flxValue.AsBool}";
            }

            if (type == Type.Blob)
            {
                return $"{_key}{_flxValue.ToJson}";
            }

            if (type == Type.Float || type == Type.IndirectFloat)
            {
                return $"{_key}{_flxValue.AsDouble}";
            }

            if (type == Type.Int || type == Type.IndirectInt)
            {
                return $"{_key}{_flxValue.AsLong}";
            }

            if (type == Type.Uint || type == Type.IndirectUInt)
            {
                return $"{_key}{_flxValue.AsULong}";
            }

            if (type == Type.String)
            {
                return $"{_key}'{_flxValue.AsString}'";
            }

            return "UNKNOWN";
        }
    }
    public override int depth => _depth;
    public override bool hasChildren => TypesUtil.IsAVector(_flxValue.ValueType) || _flxValue.ValueType == Type.Map;

    public override List<TreeViewItem> children
    {
        get
        {
            if (TypesUtil.IsAVector(_flxValue.ValueType))
            {
                var vec = _flxValue.AsVector;
                if (_children == null)
                {
                    _children = new List<TreeViewItem>(vec.Length);
                    var index = 0;
                    foreach (var item in vec)
                    {
                        if (_query != null)
                        {
                            var confirms = _query.Constraint.Confirms(vec, index);
                            if (confirms)
                            {
                                _children.Add(new FlxValueTreeViewItem(item, _depth+1, this, $"{index} : ", _query.Propagating ? _query : _query.Next));
                            } else if (_query.Optional)
                            {
                                _children.Add(new FlxValueTreeViewItem(item, _depth+1, this, $"{index} : ", _query));
                            }
                        }
                        else
                        {
                            _children.Add(new FlxValueTreeViewItem(item, _depth+1, this, $"{index} : "));    
                        }
                        
                        index++;
                    }
                }

                return _children;
            }

            if (_flxValue.ValueType == Type.Map)
            {
                var map = _flxValue.AsMap;
                if (_children == null)
                {
                    _children = new List<TreeViewItem>(map.Length);
                    foreach (var item in map)
                    {
                        if (_query != null)
                        {
                            var confirms = _query.Constraint.Confirms(map, item.Key);
                            if (confirms)
                            {
                                _children.Add(new FlxValueTreeViewItem(item.Value, _depth+1, this, $"{item.Key} : ", _query.Next));
                            } else if (_query.Optional)
                            {
                                _children.Add(new FlxValueTreeViewItem(item.Value, _depth+1, this, $"{item.Key} : ", _query));
                            }
                        }
                        else
                        {
                            _children.Add(new FlxValueTreeViewItem(item.Value, _depth+1, this, $"{item.Key} : "));
                        }
                    }
                }

                return _children;
            }
            
            return new List<TreeViewItem>();
        }
    }

    public override TreeViewItem parent => _parent;
}