﻿using System;
using System.Collections.Generic;

namespace Jurassic.Compiler
{
    /// <summary>
    /// Represents a generated method and it's dependencies.  For internal use only.
    /// </summary>
    public class GeneratedMethod
    {
        /// <summary>
        /// Creates a new GeneratedMethod instance.
        /// </summary>
        /// <param name="delegateToMethod"> A delegate that refers to the generated method. </param>
        /// <param name="dependencies"> A list of dependent generated methods.  Can be <c>null</c>. </param>
        public GeneratedMethod(Delegate delegateToMethod, IList<GeneratedMethod> dependencies)
        {
            if (delegateToMethod == null)
                throw new ArgumentNullException("delegateToMethod");
            this.GeneratedDelegate = delegateToMethod;
            this.Dependencies = dependencies;
        }

        /// <summary>
        /// Gets a delegate which refers to the generated method.
        /// </summary>
        public Delegate GeneratedDelegate
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a list of dependent generated methods.
        /// </summary>
        public IList<GeneratedMethod> Dependencies
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the disassembled IL code for the method.
        /// </summary>
        public string DisassembledIL
        {
            get;
            set;
        }



        private static Dictionary<long, WeakReference> generatedMethodCache;
        private static object cacheLock = new object();
        private static long generatedMethodID;
        private const int compactGeneratedCacheCount = 100;

        /// <summary>
        /// Retrieves the code for a generated method, given the ID.  For internal use only.
        /// </summary>
        /// <param name="id"> The ID of the generated method. </param>
        /// <returns> A <c>GeneratedMethodInfo</c> instance. </returns>
        public static GeneratedMethod Load(long id)
        {
            lock (cacheLock)
            {
                if (generatedMethodCache == null)
                    throw new InvalidOperationException("Internal error: no generated method cache available.");
                WeakReference generatedMethodReference;
                if (generatedMethodCache.TryGetValue(id, out generatedMethodReference) == false)
                    throw new InvalidOperationException(string.Format("Internal error: generated method {0} was garbage collected.", id));
                var generatedMethod = (GeneratedMethod)generatedMethodReference.Target;
                if (generatedMethod == null)
                    throw new InvalidOperationException(string.Format("Internal error: generated method {0} was garbage collected.", id));
                return generatedMethod;
            }
        }


        public static Stack<long> generatedMethodIDStack = null;

        public static long Prepare()
        {
            long id = generatedMethodID;
            generatedMethodID++;

            if (generatedMethodIDStack == null)
                generatedMethodIDStack = new Stack<long>();
            generatedMethodIDStack.Push(id);

            return id;
        }
        

        /// <summary>
        /// Saves the given generated method and returns an ID.  For internal use only.
        /// </summary>
        /// <param name="generatedMethod"> The generated method to save. </param>
        /// <returns> The ID that was associated with the generated method. </returns>
        public static long Save(GeneratedMethod generatedMethod)
        {
            if (generatedMethod == null)
                throw new ArgumentNullException("generatedMethod");
            lock (cacheLock)
            {
                // Create a cache (if it hasn't already been created).
                if (generatedMethodCache == null)
                    generatedMethodCache = new Dictionary<long, WeakReference>();

                // Create a weak reference to the generated method and add it to the cache.
                long id = generatedMethodID;

                if (generatedMethodIDStack != null)
                    id = generatedMethodIDStack.Pop(); // Use value from stack when compiling
                else
                    generatedMethodID++; // Increment the ID for next time.

                var weakReference = new WeakReference(generatedMethod);
                generatedMethodCache.Add(id, weakReference);

                // Every X calls to this method, compact the cache by removing any weak references that
                // point to objects that have been collected.
                if (generatedMethodID % compactGeneratedCacheCount == 0)
                {
                    // Remove any weak references that have expired.
                    var expiredIDs = new List<long>();
                    foreach (var pair in generatedMethodCache)
                        if (pair.Value.Target == null)
                            expiredIDs.Add(pair.Key);
                    foreach (int expiredID in expiredIDs)
                        generatedMethodCache.Remove(expiredID);
                }

                // Return the ID that was allocated.
                return id;
            }
        }



        /// <summary>
        /// Creates a new GeneratedMethod instance without a delegate method.
        /// </summary>
        /// <param name="dependencies"> A list of dependent generated methods.  Can be <c>null</c>. </param>
        public GeneratedMethod(IList<GeneratedMethod> dependencies)
        {
            this.Dependencies = dependencies;
        }


        private static List<GeneratedMethod> protectedMethodList;

        /// <summary>
        /// Load the generatedMethodCache from an Assembly, instead of compiling the methods
        /// </summary>
        /// <param name="assembly">The assembly to load the methods from</param>
        /// <param name="prefix">Prefix name for generated types</param>
        public static void LoadCacheFromType(Type type, string prefix)
        {
            generatedMethodCache = new Dictionary<long, WeakReference>();
            protectedMethodList = new List<GeneratedMethod>();
            
            foreach (System.Reflection.MethodInfo method in type.GetMethods())
            {
                if (method.Name.StartsWith(prefix))
                {
                    long id = Convert.ToInt64(method.Name.Substring(prefix.Length));
                    
                    var delegateMethod = Delegate.CreateDelegate(typeof(Library.FunctionDelegate), method);
                    var generatedMethod = new GeneratedMethod(delegateMethod, new List<GeneratedMethod>());

                    var weakReference = new WeakReference(generatedMethod);
                    generatedMethodCache.Add(id, weakReference);
                    protectedMethodList.Add(generatedMethod); // Protect from GC
                }
            }
        }


    }
}
