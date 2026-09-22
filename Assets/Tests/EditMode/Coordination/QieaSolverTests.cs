using System;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Solvers;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Data.Coordination;

namespace ShadowVale.Tests.EditMode.Coordination
{
    public class QieaSolverTests
    {
        static QuboRequest Trap()
        {
            // A0: node0 reward10, node1 reward9; A1: node0 reward9, node1 reward0.
            // Greedy yields -10; swapping the assignments gives exact optimum -18.
            var q=new double[16];q[0]=-110;q[5]=-109;q[10]=-109;q[15]=-100;
            q[1]=200;q[11]=200;q[2]=200;q[7]=200;
            return new QuboRequest{AgentCount=2,NodeCount=2,QMatrix=q,Offset=200,Seed=42};
        }
        [Test] public void RotationSearchEscapesGreedyTrapAndReturnsExactOptimum()
        {
            var request=Trap();var greedy=new GreedySolver().Solve(request);var plan=new QieaSolver(1000,30).Solve(request);
            Assert.AreEqual(-10,greedy.Energy,1e-9);Assert.AreEqual(-18,plan.Energy,1e-9);
            CollectionAssert.AreEqual(new[]{1,0},plan.AgentToTarget);
            var x=new[]{false,true,true,false};Assert.AreEqual(GreedySolver.Energy(request.QMatrix,x,4)+request.Offset,plan.Energy,1e-9);
        }
        [Test] public void SymmetricAndUpperTriangularEncodingAgree()
        {
            var upper=Trap();var symmetric=Trap();
            for(int i=0;i<4;i++)for(int j=0;j<4;j++)if(i!=j)symmetric.QMatrix[i*4+j]=(upper.QMatrix[i*4+j]+upper.QMatrix[j*4+i])/2;
            var a=new QieaSolver(1000,30).Solve(upper);var b=new QieaSolver(1000,30).Solve(symmetric);
            Assert.AreEqual(a.Energy,b.Energy,1e-9);CollectionAssert.AreEqual(a.AgentToTarget,b.AgentToTarget);
        }
        [Test] public void SeededFixedIterationsAreReproducibleAndRawSamplesAreReported()
        {
            var a=new QieaSolver(1000,10,8).Solve(Trap());var b=new QieaSolver(1000,10,8).Solve(Trap());
            CollectionAssert.AreEqual(a.AgentToTarget,b.AgentToTarget);Assert.AreEqual(80,a.SamplesEvaluated);
            Assert.That(a.RawFeasibleSamples,Is.InRange(0,a.SamplesEvaluated));Assert.AreEqual(a.RawFeasibleSamples,b.RawFeasibleSamples);
        }
        [Test] public void TooFewNodesFailsClearlyInsteadOfIndexingMinusOne()
        {
            Assert.Throws<ArgumentException>(()=>new GreedySolver().Solve(new QuboRequest{AgentCount=3,NodeCount=2,QMatrix=new double[36]}));
        }
        [Test] public void FactoryReturnsActualLocalQieaWithoutSilentGreedyFallback()
        {
            var solver=SolverFactory.Create("qiea",4,out var reason);Assert.AreEqual("qiea",solver.VariantId);Assert.IsNull(reason);
        }
    }
}
