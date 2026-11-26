namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// "Minhas inscrições" diz, por inscrição, por que as inscrições do processo estão fechadas, no instante do relógio
/// fixo da consulta: o motivo é do processo, e não da situação da inscrição.
/// </summary>
public sealed class MotivoDasInscricoesFechadasNaListaTests
{
    private static readonly Cpf Candidata = PersonasDeTeste.FernandaDrumond;
    private static readonly DateTimeOffset NoPeriodo = DivulgacaoDeTeste.DentroDoPeriodo;
    private static readonly DateTimeOffset DepoisDoPeriodo = DivulgacaoDeTeste.FimDoPeriodo.AddDays(10);
    private static readonly DateTimeOffset AntesDoPeriodo = DivulgacaoDeTeste.InicioDoPeriodo.AddDays(-10);

    private static CodigoComRotulo<MotivoDasInscricoesFechadas> Motivo(MotivoDasInscricoesFechadas motivo) =>
        CodigoComRotulo<MotivoDasInscricoesFechadas>.De(motivo);

    private static async Task<(IReadOnlyList<MinhaInscricaoDto> Itens, ApoioDaListaDeInscricoes Apoio)> ListarAsync(
        DateTimeOffset agora, IReadOnlyList<Inscricao> inscricoes, IResolvedorFusoInstitucional? fuso = null, params ProcessoDivulgadoParaOPeriodoDeTeste[] processos)
    {
        ApoioDaListaDeInscricoes apoio = new(new RelogioFixo(agora));
        (ICertameDivulgadoRepository certames, IProcessoSeletivoRepository repositorios, IRegistroCodecsEnvelope codecs) = ProcessoDivulgadoParaOPeriodoDeTeste.Repositorios(processos);
        ProcessosDasInscricoes situacoes = apoio.Processos(certames, repositorios, codecs, fuso);

        IReadOnlyList<MinhaInscricaoDto> itens = (await ApoioDaListaDeInscricoes.ListarAsync(inscricoes, situacoes)).Value!;
        return (itens, apoio);
    }

    private static async Task<MinhaInscricaoDto> ListarUmaAsync(
        DateTimeOffset agora, ProcessoDivulgadoParaOPeriodoDeTeste processo, Func<Guid, Inscricao>? inscricao = null, IResolvedorFusoInstitucional? fuso = null)
    {
        Inscricao deTeste = (inscricao ?? (id => ApoioDaListaDeInscricoes.Rascunho(Candidata, id)))(processo.ProcessoId);
        (IReadOnlyList<MinhaInscricaoDto> itens, _) = await ListarAsync(agora, [deTeste], fuso, processo);

        return itens.Should().ContainSingle().Subject;
    }

    [Fact(DisplayName = "Depois do fim do período, a inscrição vem com o motivo período encerrado")]
    public async Task Listar_DepoisDoFim_PeriodoEncerrado()
    {
        MinhaInscricaoDto item = await ListarUmaAsync(DepoisDoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste());

        item.MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.PeriodoEncerrado));
    }

    [Fact(DisplayName = "Antes do início do período, a inscrição vem com o motivo período não iniciado")]
    public async Task Listar_AntesDoInicio_PeriodoNaoIniciado()
    {
        MinhaInscricaoDto item = await ListarUmaAsync(AntesDoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste());

        item.MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.PeriodoNaoIniciado));
    }

    [Fact(DisplayName = "Processo sem formulário de inscrição vem com o motivo processo sem inscrição pelo sistema")]
    public async Task Listar_SemFormularioDeInscricao_ProcessoSemInscricao()
    {
        MinhaInscricaoDto item = await ListarUmaAsync(NoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste { ComFormularioDeInscricao = false });

        item.MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.ProcessoSemInscricao));
    }

    [Fact(DisplayName = "Processo que deixou de estar divulgado vem com o motivo período não confirmado")]
    public async Task Listar_SemDivulgacao_PeriodoNaoConfirmado()
    {
        MinhaInscricaoDto item = await ListarUmaAsync(NoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste { Divulgado = false });

        item.MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado));
        item.NomeDoProcesso.Should().BeNull();
    }

    [Fact(DisplayName = "Versão divulgada que não se reabre e fuso que não se resolve também não confirmam o período")]
    public async Task Listar_VersaoIlegivelOuFusoIndisponivel_PeriodoNaoConfirmado()
    {
        MinhaInscricaoDto versaoIlegivel = await ListarUmaAsync(NoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste { VersaoLegivel = false });
        MinhaInscricaoDto fusoIndisponivel = await ListarUmaAsync(NoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste(), fuso: new ResolvedorFusoIndisponivelDeTeste());

        versaoIlegivel.MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado));
        fusoIndisponivel.MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado));
    }

    [Fact(DisplayName = "Com as inscrições abertas, não há motivo de fechamento e o rascunho pode continuar")]
    public async Task Listar_NoPeriodo_SemMotivo()
    {
        MinhaInscricaoDto item = await ListarUmaAsync(NoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste());

        item.MotivoDasInscricoesFechadas.Should().BeNull();
        item.Acoes.PodeContinuar.Should().BeTrue();
    }

    [Fact(DisplayName = "A inscrição enviada, com o período encerrado, vem com o motivo e sem nenhuma ação")]
    public async Task Listar_EnviadaComPeriodoEncerrado_ComMotivoESemAcoes()
    {
        MinhaInscricaoDto item = await ListarUmaAsync(DepoisDoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste(), id => ApoioDaListaDeInscricoes.Enviada(Candidata, id));

        item.Situacao.Should().Be(CodigoComRotulo<SituacaoInscricao>.De(SituacaoInscricao.Enviada));
        item.MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.PeriodoEncerrado));
        item.Acoes.Should().Be(new AcoesDaInscricaoDto(false, false, false, false));
    }

    [Fact(DisplayName = "A inscrição cancelada, com o período aberto, vem sem motivo e sem nenhuma ação")]
    public async Task Listar_CanceladaComPeriodoAberto_SemMotivoESemAcoes()
    {
        MinhaInscricaoDto item = await ListarUmaAsync(NoPeriodo, new ProcessoDivulgadoParaOPeriodoDeTeste(), id => ApoioDaListaDeInscricoes.InscricaoCancelada(Candidata, id));

        item.MotivoDasInscricoesFechadas.Should().BeNull("o motivo é do período do processo, e não da situação da inscrição");
        item.Acoes.Should().Be(new AcoesDaInscricaoDto(false, false, false, false));
    }

    [Fact(DisplayName = "Dois processos na mesma lista trazem cada um o seu motivo")]
    public async Task Listar_UmProcessoSemInscricaoEOutroEncerrado_MotivosDiferentes()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste encerrado = new();
        ProcessoDivulgadoParaOPeriodoDeTeste semInscricao = new() { ComFormularioDeInscricao = false };
        Inscricao doEncerrado = ApoioDaListaDeInscricoes.Rascunho(Candidata, encerrado.ProcessoId);
        Inscricao doSemInscricao = ApoioDaListaDeInscricoes.Rascunho(Candidata, semInscricao.ProcessoId);

        (IReadOnlyList<MinhaInscricaoDto> itens, _) = await ListarAsync(DepoisDoPeriodo, [doEncerrado, doSemInscricao], null, encerrado, semInscricao);

        itens.Single(i => i.InscricaoId == doEncerrado.Id).MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.PeriodoEncerrado));
        itens.Single(i => i.InscricaoId == doSemInscricao.Id).MotivoDasInscricoesFechadas.Should().Be(Motivo(MotivoDasInscricoesFechadas.ProcessoSemInscricao));
    }

    private static ProcessoDivulgadoParaOPeriodoDeTeste[] UmProcessoParaCadaCaso() =>
    [
        new(), new(), new() { ComFormularioDeInscricao = false }, new() { Divulgado = false }, new() { VersaoLegivel = false },
    ];

    private static Inscricao[] UmRascunhoPorProcesso(ProcessoDivulgadoParaOPeriodoDeTeste[] processos) =>
        [.. processos.Select(p => ApoioDaListaDeInscricoes.Rascunho(Candidata, p.ProcessoId))];

    [Theory(DisplayName = "O rascunho só pode continuar e ser descartado quando não há motivo de fechamento")]
    [MemberData(nameof(InstantesDaConsulta))]
    public async Task Listar_Rascunho_PodeContinuarSeESomenteSeNaoHaMotivo(DateTimeOffset agora)
    {
        ProcessoDivulgadoParaOPeriodoDeTeste[] processos = UmProcessoParaCadaCaso();

        (IReadOnlyList<MinhaInscricaoDto> itens, _) = await ListarAsync(agora, UmRascunhoPorProcesso(processos), null, processos);

        foreach (MinhaInscricaoDto item in itens)
        {
            item.Acoes.PodeContinuar.Should().Be(item.MotivoDasInscricoesFechadas is null);
            item.Acoes.PodeDescartar.Should().Be(item.MotivoDasInscricoesFechadas is null);
        }
    }

    public static TheoryData<DateTimeOffset> InstantesDaConsulta => new() { NoPeriodo, DepoisDoPeriodo, AntesDoPeriodo };

    [Fact(DisplayName = "No período, só os processos com período confirmado e formulário de inscrição estão sem motivo")]
    public async Task Listar_NoPeriodo_SoOsProcessosAbertosEstaoSemMotivo()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste[] processos = UmProcessoParaCadaCaso();

        (IReadOnlyList<MinhaInscricaoDto> itens, _) = await ListarAsync(NoPeriodo, UmRascunhoPorProcesso(processos), null, processos);

        itens.Count(static i => i.MotivoDasInscricoesFechadas == null).Should().Be(2);
    }

    [Fact(DisplayName = "Período não confirmado é avisado uma vez por processo, só com o id do processo e o código do erro")]
    public async Task Listar_PeriodoNaoConfirmado_AvisaUmaVezPorProcesso()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste semDivulgacao = new() { Divulgado = false };
        Inscricao[] inscricoes = [ApoioDaListaDeInscricoes.InscricaoComNome(Candidata, semDivulgacao.ProcessoId), ApoioDaListaDeInscricoes.Rascunho(Candidata, semDivulgacao.ProcessoId)];

        (_, ApoioDaListaDeInscricoes apoio) = await ListarAsync(NoPeriodo, inscricoes, null, semDivulgacao);

        RegistroDeLog registro = apoio.Logger.Registros.Should().ContainSingle().Subject;
        registro.Nivel.Should().Be(LogLevel.Warning);
        registro.Propriedades.Keys.Should().BeEquivalentTo(["ProcessoSeletivoId", "CodigoDoErro", "{OriginalFormat}"]);
        registro.Propriedades["ProcessoSeletivoId"].Should().Be(semDivulgacao.ProcessoId);
        registro.Propriedades["CodigoDoErro"].Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "O aviso do período não confirmado não carrega CPF, nome nem outro dado do candidato")]
    public async Task Listar_PeriodoNaoConfirmado_AvisoSemDadoPessoal()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste semDivulgacao = new() { Divulgado = false };

        (_, ApoioDaListaDeInscricoes apoio) = await ListarAsync(NoPeriodo, [ApoioDaListaDeInscricoes.InscricaoComNome(Candidata, semDivulgacao.ProcessoId)], null, semDivulgacao);

        RegistroDeLog registro = apoio.Logger.Registros.Should().ContainSingle().Subject;
        string tudoQueFoiRegistrado = registro.Mensagem + string.Join(' ', registro.Propriedades.Values);
        tudoQueFoiRegistrado.Should().NotContain(Candidata.Valor).And.NotContain(Candidata.Mascarado).And.NotContain(PersonasDeTeste.NomeFernandaDrumond);
    }

    [Fact(DisplayName = "Os outros motivos de fechamento e a inscrição aberta não geram aviso")]
    public async Task Listar_OutrosMotivosEAberto_NaoAvisam()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste encerrado = new();
        ProcessoDivulgadoParaOPeriodoDeTeste semInscricao = new() { ComFormularioDeInscricao = false };
        Inscricao[] inscricoes = [ApoioDaListaDeInscricoes.Rascunho(Candidata, encerrado.ProcessoId), ApoioDaListaDeInscricoes.Rascunho(Candidata, semInscricao.ProcessoId)];

        (_, ApoioDaListaDeInscricoes apoio) = await ListarAsync(DepoisDoPeriodo, inscricoes, null, encerrado, semInscricao);
        (_, ApoioDaListaDeInscricoes apoioAberto) = await ListarAsync(NoPeriodo, [ApoioDaListaDeInscricoes.Rascunho(Candidata, encerrado.ProcessoId)], null, encerrado);

        apoio.Logger.Registros.Should().BeEmpty();
        apoioAberto.Logger.Registros.Should().BeEmpty();
    }
}
