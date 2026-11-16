namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os portões do pedido de identificação do processo: identificador, localidade, contagem e calendário.</summary>
internal sealed record PortoesDeIdentificacao(
    Func<DomainError?> IdentificadorLegivel,
    Func<DomainError?> Localidade,
    Func<DomainError?> AlgoritmoDeContagem,
    Func<DomainError?> CalendarioVigente)
{
    internal static PortoesDeIdentificacao Para(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido) => new(
        IdentificadorLegivel: () => ContagemDePrazos.PendenciaDoIdentificadorLegivel(estado),
        Localidade: () => ContagemDePrazos.PendenciaDaLocalidade(estado),
        AlgoritmoDeContagem: () => ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(estado),
        CalendarioVigente: () => ContagemDePrazos.PendenciaDoCalendarioVigente(estado, pedido.Contexto));

    internal Func<DomainError?>[] EmOrdem() => [IdentificadorLegivel, Localidade, AlgoritmoDeContagem, CalendarioVigente];
}

/// <summary>Os portões da estrutura da configuração: conformidade, cronograma, procedimentos de recurso, bancas e correção independente.</summary>
internal sealed record PortoesDaEstrutura(
    Func<DomainError?> Conformidade,
    Func<DomainError?> Cronograma,
    Func<DomainError?> ProcedimentosDeRecurso,
    Func<DomainError?> Bancas,
    Func<DomainError?> CorrecaoIndependente)
{
    internal static PortoesDaEstrutura Para(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido) => new(
        Conformidade: () => ChecklistDeConformidade.PendenciaDeConformidade(estado),
        Cronograma: () => PendenciasDoCronograma.PendenciaDoCronograma(estado, pedido.Contexto.FusoInstitucional),
        ProcedimentosDeRecurso: () => PendenciasDosProcedimentosDeRecurso.PendenciaDosProcedimentosDeRecurso(estado),
        Bancas: () => PendenciasDasBancas.PendenciaDasBancas(estado, pedido.Corretores),
        CorrecaoIndependente: () => PendenciasDaCorrecaoIndependente.PendenciaDaCorrecaoIndependente(estado, pedido.Corretores));

    internal Func<DomainError?>[] EmOrdem() => [Conformidade, Cronograma, ProcedimentosDeRecurso, Bancas, CorrecaoIndependente];
}

/// <summary>Os portões dos tipos de ato que o cronograma declara.</summary>
internal sealed record PortoesDosAtos(
    Func<DomainError?> TipoDeAtoUnico,
    Func<DomainError?> ResultadoSemPapel,
    Func<DomainError?> TipoQueCongelaDeclaradoComoResultado)
{
    internal static PortoesDosAtos Para(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido) => new(
        TipoDeAtoUnico: () => TiposDeAtoDoCronograma.PendenciaDoTipoDeAtoUnico(estado, pedido.TiposDeAtoUnicos),
        ResultadoSemPapel: () => TiposDeAtoDoCronograma.PendenciaDoResultadoSemPapel(estado, pedido.TiposDeAtoUnicos),
        TipoQueCongelaDeclaradoComoResultado: () => TiposDeAtoDoCronograma.PendenciaDoTipoQueCongelaDeclaradoComoResultado(estado, pedido.TiposDeAtoUnicos));

    internal Func<DomainError?>[] EmOrdem() => [TipoDeAtoUnico, ResultadoSemPapel, TipoQueCongelaDeclaradoComoResultado];
}

/// <summary>Os portões que fecham a configuração: a cascata e as checagens antes da canonicalização.</summary>
internal sealed record PortoesDoEncerramento(
    Func<DomainError?> Cascata,
    Func<DomainError?> PreCanonicalizacao)
{
    internal static PortoesDoEncerramento Para(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido) => new(
        Cascata: () => CoberturaDaCascata.PendenciaDaCascata(estado),
        PreCanonicalizacao: () => ProntidaoParaCongelar.PendenciaPreCanonicalizacao(estado, pedido.FatosDeModalidade));

    internal Func<DomainError?>[] EmOrdem() => [Cascata, PreCanonicalizacao];
}
