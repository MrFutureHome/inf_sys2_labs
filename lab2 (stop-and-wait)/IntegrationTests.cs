using lab2__stop_and_wait_;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TestClass]
public class IntegrationTests
{
    [TestMethod]
    public void SuccessWithoutLoss()
    {
        var s = MakeSender(0, 0);
        s.Send("hello", 0);
        Assert.AreEqual(SenderState.Idle, s.State);
        Assert.AreEqual(0, s.Retries);
    }

    [TestMethod]
    public void PacketLossRequiresRetry()
    {
        var s = MakeSender(pPkt: 0.9, pAck: 0);
        s.Send("X", 0);
        Assert.IsTrue(s.Retries > 0);
    }

    [TestMethod]
    public void AckLossRequiresRetry()
    {
        var s = MakeSender(pPkt: 0, pAck: 0.9);
        s.Send("Y", 0);
        Assert.IsTrue(s.Retries > 0);
    }

    [TestMethod]
    public void MixedLossStillDelivers()
    {
        var s = MakeSender(pPkt: 0.4, pAck: 0.4, seed: 7);
        s.Send("Z", 0);
        Assert.AreEqual(SenderState.Idle, s.State);
    }

    static Sender MakeSender(double pPkt,
                             double pAck,
                             int seed = 100)
        => new Sender(new Receiver(),
                      new DeterministicRnd(seed),
                      new NoDelay(),
                      pPkt, pAck);
}
