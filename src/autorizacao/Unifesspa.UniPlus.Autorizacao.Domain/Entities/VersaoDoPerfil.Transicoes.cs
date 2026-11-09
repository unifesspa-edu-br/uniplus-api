namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As transições de situação da versão e o que o cancelamento, a expiração e a aprovação deixam gravado nela.</summary>
public sealed partial class VersaoDoPerfil
{
    public FormaDoCancelamento? FormaDoCancelamento { get; private set; }

    public string? MotivoDoCancelamento { get; private set; }

    public string? CanceladaPorEmissor { get; private set; }

    public string? CanceladaPorSub { get; private set; }

    public string? CanceladaPorNome { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    public DateTimeOffset? ExpiradaEm { get; private set; }

    /// <summary>A gestão de acessos já aprovou a versão no ciclo atual.</summary>
    public bool AprovadaPelaGestao =>
        DecisaoDaEtapa(EtapaDaVersao.GestaoDeAcessos) is { Resultado: ResultadoDaDecisao.Aprovado };

    /// <summary>A decisão dada à etapa no ciclo atual; nula enquanto a etapa não foi decidida.</summary>
    public DecisaoDaVersao? DecisaoDaEtapa(EtapaDaVersao etapa) =>
        _decisoes.Find(d => d.Ciclo == CicloDeAprovacao && d.Etapa == etapa);

    /// <summary>As decisões do ciclo atual.</summary>
    public IReadOnlyList<DecisaoDaVersao> DecisoesDoCiclo() => [.. _decisoes.Where(d => d.Ciclo == CicloDeAprovacao)];

    /// <summary>A pendente que passou do prazo: de <see cref="ExpiraEm"/> em diante a versão não aprova mais.</summary>
    public bool Expirou(DateTimeOffset agora) => Pendente && ExpiraEm <= agora;

    /// <summary>A recusa de operar uma versão que não está pendente: o prazo, se ela expirou; senão, a situação dela.</summary>
    internal DomainError RecusaPorNaoPendente() =>
        Expirada ? PerfilErros.VersaoExpirada() : PerfilErros.VersaoNaoPendente();

    internal void RegistrarDecisao(DecisaoDaVersao decisao) => _decisoes.Add(decisao);

    /// <summary>Descarta as aprovações do ciclo: a classe mudou, e o ciclo novo recomeça pela gestão de acessos.</summary>
    internal RecalculoDaAprovacao SubirCiclo(bool exigeAgora, DateTimeOffset instante)
    {
        RecalculoDaAprovacao recalculo = RecalculoDaAprovacao.Registrar(Id, CicloDeAprovacao, ExigeProtecaoDeDados, exigeAgora, instante);
        _recalculos.Add(recalculo);
        ExigeProtecaoDeDados = exigeAgora;
        CicloDeAprovacao++;

        return recalculo;
    }

    /// <summary>Retira a versão pendente. Quem chama confere que ela está pendente e, na forma pessoa, o motivo.</summary>
    internal void Cancelar(Identidade autor, string? motivo, FormaDoCancelamento forma, DateTimeOffset instante)
    {
        Situacao = SituacaoDaVersao.Cancelada;
        FormaDoCancelamento = forma;
        MotivoDoCancelamento = motivo;
        CanceladaPorEmissor = autor.Emissor;
        CanceladaPorSub = autor.Sub;
        CanceladaPorNome = autor.Nome;
        CanceladaEm = instante;
    }

    /// <summary>Materializa o fim do prazo, no instante em que o prazo terminou, e não no da operação que o notou.</summary>
    internal void Expirar()
    {
        Situacao = SituacaoDaVersao.Expirada;
        ExpiradaEm = ExpiraEm;
    }

    internal void Recusar() => Situacao = SituacaoDaVersao.Recusada;

    internal void EntrarEmVigor() => Situacao = SituacaoDaVersao.Vigente;

    internal void Encerrar() => Situacao = SituacaoDaVersao.Encerrada;
}
