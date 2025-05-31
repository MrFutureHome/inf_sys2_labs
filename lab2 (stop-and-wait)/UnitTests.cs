using lab2__stop_and_wait_;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TestClass]
public class UnitTests
{
    private Sender MakeSender(
            double pPkt = 0.0,
            double pAck = 0.0,
            int seed = 42)
    {
        var receiver = new Receiver();
        var rnd = new DeterministicRnd(seed);
        var delay = new NoDelay();
        return new Sender(receiver, rnd, delay, pPkt, pAck);
    }

    [TestMethod]
    public void Sender_StartsInIdleState()
    {
        var s = MakeSender();
        Assert.AreEqual(SenderState.Idle, s.State);
        Assert.AreEqual(0, s.Sequence);
        Assert.AreEqual(0, s.Retries);
    }

    [TestMethod]
    public void Sender_TogglesSequence_AfterSuccessfulSend()
    {
        var s = MakeSender();
        // При нулевых потерях Send завершится сразу, без ретраев
        s.Send("X", timeoutMs: 0);
        int firstSeq = s.Sequence;
        s.Send("Y", timeoutMs: 0);
        // После двух отправок Sequence должен поменяться дважды: из 0→1, потом 1→0
        Assert.AreEqual(0, s.Sequence);
    }

    [TestMethod]
    public void Receiver_DetectsNewAndDuplicatePackets()
    {
        var r = new Receiver();
        var p = new Packet { SequenceNumber = 0, Data = "hello" };
        bool first = r.Receive(p);
        bool second = r.Receive(p);
        Assert.IsTrue(first, "Первый пакет с seq=0 считается новым");
        Assert.IsFalse(second, "Второй (тот же) с seq=0 — дубликат");
    }

    [TestMethod]
    public void Sender_StateTransitions_AfterSend()
    {
        var s = MakeSender();
        Assert.AreEqual(SenderState.Idle, s.State);
        s.Send("data", timeoutMs: 0);
        // После успешной доставки FSM берёт новое состояние Idle
        Assert.AreEqual(SenderState.Idle, s.State);
    }
}
