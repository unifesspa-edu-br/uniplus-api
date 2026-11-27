namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A providência como a API a expõe, na leitura e na trilha da concessão.</summary>
public sealed class ProvidenciaMappingTests
{
    private static readonly DateTimeOffset Agora = ProvidenciaBuilder.Agora;

    [Fact(DisplayName = "A aberta cuja concessão foi revogada sai prejudicada, com a revogação como encerramento")]
    public void Detalhe_Prejudicada_TrazOEncerramento()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();
        cenario.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(5)), "Revogada pela revogação comum.");

        ProvidenciaDto dto = ProvidenciaMapping.ToDto(Item(p, cenario), Agora.AddMinutes(10), ProvidenciaBuilder.Diogo);

        (dto.Situacao.Codigo, dto.EncerramentoDaConcessao, dto.Concessao.Ativa).Should().Be(("PREJUDICADA", Encerramento(CausaDoEncerramento.Revogada, Agora.AddMinutes(5)), false));
        (dto.Fundamento, dto.Desfecho, dto.ImpedidoDeResponder).Should().Be((ProvidenciaBuilder.Fundamento, (DesfechoDaProvidenciaDto?)null, false));
    }

    [Fact(DisplayName = "A aberta cuja concessão venceu sai prejudicada, com o vencimento como encerramento")]
    public void Detalhe_Vencida_TrazOVencimento()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1)));

        ProvidenciaDto dto = ProvidenciaMapping.ToDto(Item(cenario.Aberta(), cenario), Agora.AddHours(2), null);

        (dto.Situacao.Codigo, dto.EncerramentoDaConcessao).Should().Be(("PREJUDICADA", Encerramento(CausaDoEncerramento.Vencida, Agora.AddHours(1))));
    }

    [Fact(DisplayName = "O item da fila não traz o fundamento nem o motivo do desfecho, e marca quem lê se está impedido")]
    public void Resumo_SemTextoLivre()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Respondida();

        ProvidenciaResumoDto resumo = ProvidenciaMapping.ToResumo(Item(p, cenario), Agora, ProvidenciaBuilder.Cecilia.Identidade);

        (resumo.Situacao.Codigo, resumo.Desfecho, resumo.ImpedidoDeResponder).Should().Be(("RESPONDIDA", new AtoDoDesfechoDto(Agora, new IdentidadeDto(ProvidenciaBuilder.Emissor, "diogo.souza", ProvidenciaBuilder.Diogo.Nome)), true));
        resumo.EncerramentoDaConcessao.Should().BeNull();
    }

    [Fact(DisplayName = "A trilha da concessão mostra a solicitação, com o tipo e o fundamento, e o atendimento, com o motivo")]
    public void Trilha_SolicitacaoEAtendimento()
    {
        Providencia p = new ProvidenciaBuilder().DoTipo("REVISAO").Atendida();

        EventoDaTrilhaDto[] eventos = [.. ProvidenciaMapping.EventosDaTrilha(p)];

        eventos.Select(static e => (e.Tipo.Codigo, e.Detalhe)).Should().Equal(
            ("PROVIDENCIA_SOLICITADA", $"Revisão do acesso. Fundamento: {ProvidenciaBuilder.Fundamento}"), ("PROVIDENCIA_ATENDIDA", ProvidenciaBuilder.Motivo));
        eventos.Select(static e => e.Autor!.ContaId).Should().Equal(ProvidenciaBuilder.Cecilia.Identidade.ContaId, ProvidenciaBuilder.Diogo.ContaId);
    }

    [Fact(DisplayName = "A trilha da concessão mostra a resposta, e a aberta só a solicitação")]
    public void Trilha_RespostaEAberta()
    {
        ProvidenciaMapping.EventosDaTrilha(new ProvidenciaBuilder().Respondida()).Select(static e => e.Tipo.Codigo).Should().Equal("PROVIDENCIA_SOLICITADA", "PROVIDENCIA_RESPONDIDA");
        ProvidenciaMapping.EventosDaTrilha(new ProvidenciaBuilder().Aberta()).Select(static e => e.Tipo.Codigo).Should().Equal("PROVIDENCIA_SOLICITADA");
    }

    [Fact(DisplayName = "A trilha da própria concessão mostra o vencimento como evento do sistema, e a revogação no lugar dele quando revogada")]
    public void TrilhaDaConcessao_VencimentoERevogacao()
    {
        Concessao vencida = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        Concessao revogada = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        revogada.Revogar(ProvidenciaBuilder.AtoDeDiogo(), "Mudou de função.");

        EventoDaTrilhaDto fim = ConcessaoMapping.EventosDaConcessao(vencida, Agora.AddHours(2)).Last();
        (fim.Tipo.Codigo, fim.Instante, fim.Autor, fim.Ator.Codigo).Should().Be(("VENCIDA", Agora.AddHours(1), (IdentidadeDto?)null, "SISTEMA"));
        ConcessaoMapping.EventosDaConcessao(revogada, Agora.AddHours(2)).Select(static e => e.Tipo.Codigo).Should().Equal("CONCEDIDA", "REVOGADA");
        ConcessaoMapping.EventosDaConcessao(vencida, Agora).Select(static e => e.Tipo.Codigo).Should().Equal("CONCEDIDA");
    }

    [Fact(DisplayName = "A trilha da concessão vencida e revogada depois mostra o vencimento e a revogação, e a providência prejudicada aponta o vencimento")]
    public void VencidaERevogadaDepois_TrilhaEEncerramento()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1)));
        Providencia p = cenario.Aberta();
        cenario.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddHours(3)), "Revogada depois de vencida.");

        ConcessaoMapping.EventosDaConcessao(cenario.Concessao, Agora.AddHours(4)).Select(static e => (e.Tipo.Codigo, e.Instante)).Should().Equal(
            ("CONCEDIDA", Agora.AddDays(-30)), ("VENCIDA", Agora.AddHours(1)), ("REVOGADA", Agora.AddHours(3)));
        ProvidenciaMapping.ToDto(Item(p, cenario), Agora.AddHours(4), null).EncerramentoDaConcessao
            .Should().Be(Encerramento(CausaDoEncerramento.Vencida, Agora.AddHours(1)));
    }

    [Fact(DisplayName = "O detalhe da providência aberta mostra o tipo, a situação e o escopo da instituição da concessão como código e rótulo")]
    public void Detalhe_Aberta_MostraTipoSituacaoEEscopoComRotulo()
    {
        ProvidenciaBuilder cenario = new();

        ProvidenciaDto dto = ProvidenciaMapping.ToDto(Item(cenario.Aberta(), cenario), Agora, null);

        (dto.Tipo.Codigo, dto.Tipo.Rotulo, dto.Situacao.Codigo, dto.Situacao.Rotulo, dto.Concessao.Escopo.Tipo.Codigo, dto.Concessao.Escopo.Tipo.Rotulo)
            .Should().Be(("RETIRADA", "Retirada do acesso", "ABERTA", "Aberta", "INSTITUICAO", "Instituição"));
    }

    [Fact(DisplayName = "O item da fila da providência de revisão mostra o tipo e a situação como código e rótulo")]
    public void Resumo_Revisao_MostraTipoESituacaoComRotulo()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().DoTipo("REVISAO");

        ProvidenciaResumoDto resumo = ProvidenciaMapping.ToResumo(Item(cenario.Respondida(), cenario), Agora, null);

        (resumo.Tipo.Codigo, resumo.Tipo.Rotulo, resumo.Situacao.Codigo, resumo.Situacao.Rotulo)
            .Should().Be(("REVISAO", "Revisão do acesso", "RESPONDIDA", "Respondida"));
    }

    [Fact(DisplayName = "A causa do encerramento mostra o código e o rótulo")]
    public void Detalhe_Prejudicada_MostraACausaComRotulo()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();
        cenario.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(5)), "Revogada pela revogação comum.");

        EncerramentoDaConcessaoDto encerramento = ProvidenciaMapping.ToDto(Item(p, cenario), Agora.AddMinutes(10), null).EncerramentoDaConcessao!;

        (encerramento.Causa.Codigo, encerramento.Causa.Rotulo).Should().Be(("REVOGADA", "Revogada"));
    }

    private static EncerramentoDaConcessaoDto Encerramento(CausaDoEncerramento causa, DateTimeOffset em) =>
        new(CodigoComRotulo<CausaDoEncerramento>.De(causa), em);

    private static ProvidenciaComConcessao Item(Providencia p, ProvidenciaBuilder cenario) =>
        new(p, new ConcessaoDaProvidencia(cenario.Concessao, NomeDoPerfil.Reidratar(CenarioDaProvidencia.PerfilNome)));
}
