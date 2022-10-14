using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.XR;

namespace Inventory
{

    // A (c++) draft implementation can found here:
    // https://gist.github.com/simplyWiri/354b08f20aa9404141f8393f40167a89

    public interface ICandidateElement<T>
    {
        float Score();
        bool ConflictsWith(T other);
    };

    // `T` is the generic class used to resolve conflicts.
    public class WeightedConflictResolver<T> where T : class
                                                     , ICandidateElement<T>
                                                     , IComparable<T> {
        struct ResultPair {
            public float score;
            public List<T> elements;

            public ResultPair(float score, List<T> elements) {
                this.score = score;
                this.elements = elements;
            }
        }
        
        public static List<T> FindOptimalSolution(List<T> elements) {
            var partitionElements = Partition(elements);
            return Resolve(partitionElements);
        }

        // To reduce the combinatorial complexity of the above problem, we (maybe) can divide the
        // problem into a smaller set of problems which can be solved in isolation, then their results
        // returned and collapsed into the solution for the problem.
        //
        // We do this by effectively attempting to isolate each element inside `elements`, into the group
        // of elements which overlap with them, and on recursively. I.e. all items which touch either the element,
        // or another element that this touches will be partitioned into the same list.
        private static List<List<T>> Partition(List<T> elements) {
            var elementToColourMap = new Dictionary<T, int>();

            int colour = 0;
            foreach(var elem in elements) {
                if ( !elementToColourMap.TryGetValue(elem, out var elemColour) ) {
                    elementToColourMap.Add(elem, colour);
                    elemColour = colour;
                    colour++;
                }

                foreach (var nestedElem in elements) {
                    // If its the same element as the higher level iteration, or it doesn't overlap with the current 
                    // element we are considering.
                    if ( elem == nestedElem || !elem.ConflictsWith(nestedElem) ) {
                        continue;
                    }

                    // Set it, and all elements which it touches (directly or indirectly) to have the same colour as the 
                    // current element.
                    if ( elementToColourMap.TryGetValue(nestedElem, out var nestedElemColour) ) {
                        var keysToFixup = elementToColourMap.Where((k, v) => v == nestedElemColour)
                                                            .Select((k, v) => k.Key)
                                                            .ToList();

                        foreach(var key in keysToFixup) {
                            elementToColourMap[key] = elemColour;
                        }

                    } else {
                        elementToColourMap.Add(nestedElem, elemColour);
                    }
                }
            }


            var partitionedElements = new List<List<T>>();
            for(var i = 0; i < colour; i++) {
                partitionedElements.Add(new List<T>());
            }

            foreach(var (key, value) in elementToColourMap) {
                partitionedElements[value].Add(key);
            }

            return partitionedElements.Where(p => p.Count != 0).ToList();
        }

        // `Recurse` is a brute force iteration approach, we do this for each 'isolated' set of elements which
        // intersect with eachother
        private static List<T> Resolve(List<List<T>> partitionedElements) {
            var elements = new List<T>();

            foreach(var list in partitionedElements) {
                // list by ref, chosenSet by copy.
                var resultPair = Recurse(new List<T>(), list);
                elements.AddRange(resultPair.elements);
            }

            return elements;
        }

        private static ResultPair Recurse(List<T> chosenSet, List<T> remainingSet) {
            if (remainingSet.Count == 0) {
                return new ResultPair( 0, chosenSet );
            }

            var lastElement = remainingSet.Last();
            remainingSet.RemoveAt(remainingSet.Count - 1);

            foreach(var elem in chosenSet) {
                if (lastElement.ConflictsWith(elem)) {
                    var pair = Recurse(chosenSet, remainingSet);
                    remainingSet.Add(lastElement);
                    return pair;
                }
            }

            // If we don't add this element, we need to intentionally make a copy of `chosenSet`, remainingSet can be passed by
            // reference.   
            var resultNoInclude = Recurse(chosenSet.ToList(), remainingSet);

            // if we did add this element
            chosenSet.Add(lastElement);
            var resultWhenInclude = Recurse(chosenSet, remainingSet);
            resultWhenInclude.score += lastElement.Score();

            remainingSet.Add(lastElement);

            return resultNoInclude.score > (resultWhenInclude.score) ? resultNoInclude : resultWhenInclude;
        }

    }


}
