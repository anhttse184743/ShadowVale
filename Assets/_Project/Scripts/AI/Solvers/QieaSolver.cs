using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Data.Coordination;

namespace ShadowVale.AI.Solvers
{
    /// <summary>Local quantum-inspired rotation-gate search, not a quantum circuit simulator.
    /// Bernoulli Q-bit observations are repaired to one node/agent and unit node capacity.
    /// The greedy incumbent is retained on deadline; no unbounded work runs on Unity's main thread.</summary>
    public sealed class QieaSolver : ISquadSolver
    {
        public string VariantId => "qiea";
        public int LatencyBudgetMs { get; }
        public int Iterations { get; }
        public int Population { get; }
        public QieaSolver(int latencyBudgetMs = 4, int iterations = 80, int population = 8)
        { LatencyBudgetMs = Math.Max(1, latencyBudgetMs); Iterations = Math.Max(1, iterations); Population = Math.Max(2, population); }
        public Task<CoordinationPlan> SolveAsync(QuboRequest request, CancellationToken ct)
            => Task.Run(() => Solve(request, ct), ct);
        public CoordinationPlan Solve(QuboRequest req, CancellationToken ct = default)
        {
            var clock = Stopwatch.StartNew();
            var best = new GreedySolver(LatencyBudgetMs).Solve(req);
            int a = req.AgentCount, n = req.NodeCount, size = req.VariableCount;
            var rng = new Random(req.Seed);
            var angles = new double[Population * size];
            for (int i=0; i<angles.Length; i++) angles[i] = Math.PI / 4;
            var measured = new bool[size]; var taken = new bool[n]; var candidate = new int[a];
            int samples=0, rawFeasible=0; bool deadline=false;
            for (int generation=0; generation<Iterations; generation++)
            {
                for (int member=0; member<Population; member++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (clock.Elapsed.TotalMilliseconds >= LatencyBudgetMs) { deadline=true; break; }
                    int offset=member*size;
                    for (int i=0; i<size; i++) { double beta=Math.Sin(angles[offset+i]); measured[i]=rng.NextDouble()<beta*beta; }
                    Array.Clear(taken,0,n); bool raw=true;
                    for (int agent=0; agent<a; agent++)
                    {
                        int count=0, observed=-1;
                        for (int node=0; node<n; node++) if(measured[agent*n+node]) {count++;observed=node;}
                        if(count!=1 || (observed>=0 && taken[observed]))raw=false;
                        if(observed>=0)taken[observed]=true;
                    }
                    if(raw)rawFeasible++;
                    Array.Clear(taken,0,n);
                    // Random cyclic order reduces a fixed first-agent advantage during repair.
                    int start=rng.Next(a);
                    for(int turn=0;turn<a;turn++)
                    {
                        int agent=(start+turn)%a, choice=-1; double gain=double.PositiveInfinity;
                        for(int pass=0;pass<2 && choice<0;pass++)
                        for(int node=0;node<n;node++)
                        {
                            if(taken[node] || (pass==0 && !measured[agent*n+node]))continue;
                            int i=agent*n+node;double e=req.QMatrix[i*size+i];
                            for(int prev=0;prev<turn;prev++){int other=(start+prev)%a,j=other*n+candidate[other];e+=req.QMatrix[i*size+j]+req.QMatrix[j*size+i];}
                            if(e<gain){gain=e;choice=node;}
                        }
                        candidate[agent]=choice;taken[choice]=true;
                    }
                    double energy=AssignmentEnergy(req,candidate);samples++;
                    if(energy<best.Energy-1e-9){best.AgentToTarget=(int[])candidate.Clone();best.Energy=energy;best.FeasibleRaw=raw;}
                    for(int i=0;i<size;i++)
                        angles[offset+i]=Math.Max(.06,Math.Min(Math.PI/2-.06,angles[offset+i]+(best.AgentToTarget[i/n]==i%n?.045:-.045)));
                }
                if(deadline)break;
            }
            best.SolverVariantId=VariantId;best.ObjectiveValue=-best.Energy;
            best.SolveLatencyMs=clock.Elapsed.TotalMilliseconds;best.HitLatencyBudget=deadline;
            best.FallbackReason=deadline?"Deadline: best feasible incumbent retained":null;
            best.SamplesEvaluated=samples;best.RawFeasibleSamples=rawFeasible;
            return best;
        }
        public static double AssignmentEnergy(QuboRequest req,int[] targets)
        {
            int size=req.VariableCount,n=req.NodeCount;double e=req.Offset;
            for(int a=0;a<targets.Length;a++){int i=a*n+targets[a];e+=req.QMatrix[i*size+i];for(int b=0;b<a;b++){int j=b*n+targets[b];e+=req.QMatrix[i*size+j]+req.QMatrix[j*size+i];}}
            return e;
        }
    }
}
