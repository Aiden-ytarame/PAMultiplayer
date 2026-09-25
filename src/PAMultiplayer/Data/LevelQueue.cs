using System;
using System.Collections.Generic;

namespace PAMultiplayer.Data;

public class LevelQueue
{
    public struct QueueEntry(string title, string id) : IEquatable<QueueEntry>
    {
        public string Title = title;
        public string Id = id;

        public bool Equals(QueueEntry other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is QueueEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id != null ? Id.GetHashCode() : 0;
        }
    }
    
    private readonly List<QueueEntry> Queue = new();
    
    public int Count => Queue.Count;

    public QueueEntry this[int i] => Queue[i];
 
    public int IndexOfLevel(string id)
    {
        for (var i = 0; i < Queue.Count; i++)
        {
            if (Queue[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
    
    public bool ContainsLevel(string id) => IndexOfLevel(id) != -1;
    public void AddLevel(string title, string id)
    {
        Queue.Add(new QueueEntry(title, id));    
    }
    
    public void RemoveLevel(string id)
    {
        for (var i = 0; i < Queue.Count; i++)
        {
            if (Queue[i].Id == id)
            {
                Queue.RemoveAt(i);
                return;
            }
        }
    }

    public void InsertLevel(string title, string id)
    {
        Queue.Insert(0, new QueueEntry(title, id));
    }

    public void RemoveLevelAt(int index)
    {
        Queue.RemoveAt(index);
    }
    
    public List<string> GetQueueLevelNames()
    {
        List<string> levelNames = new();
        for (var i = 0; i < Queue.Count; i++)
        {
            if (i > 9 && Queue.Count != 11)
            {
                levelNames.Add($"+{Queue.Count - i} Levels");
                break;
            }
                
            levelNames.Add($"{Queue[i].Title}");
        }
            
        return levelNames;
    }

    public void Clear()
    {
        Queue.Clear();
    }
}