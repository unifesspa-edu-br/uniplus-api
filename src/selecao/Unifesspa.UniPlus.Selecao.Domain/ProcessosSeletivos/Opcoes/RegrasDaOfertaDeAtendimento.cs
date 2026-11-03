namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class RegrasDaOfertaDeAtendimento
{
    // Condição e recurso são citados por código, não por Guid: só recusa se o novo conjunto de
    // ofertas deixar de conter um código hoje referenciado por condição viva.
    internal static DomainError? Recusa(EstadoDaConfiguracao estado, OfertaAtendimentoEspecializado oferta)
    {
        HashSet<string> novosCodigos = [.. oferta.Condicoes.Select(c => c.CondicaoCodigo)];
        if (ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, OfertaAtendimentoEspecializado.FatoCondicaoAtendimento, novosCodigos))
        {
            return new DomainError(
                "ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva",
                "Existe condição de gatilho documental referenciando um código de condição de atendimento que deixaria de ser ofertado — ajuste ou remova a condição antes de redefinir a oferta de atendimento.");
        }

        HashSet<string> novosRecursos = [.. oferta.Recursos.Select(static r => r.RecursoCodigo)];
        return ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, OfertaAtendimentoEspecializado.FatoRecursoAcessibilidade, novosRecursos)
            ? new DomainError(
                ProcessoSeletivoErrorCodes.RecursoAcessibilidadeReferenciadoPorCondicaoViva,
                "Existe condição referenciando um código de recurso de acessibilidade que deixaria de ser ofertado — ajuste ou remova a condição antes de redefinir a oferta de atendimento.")
            : null;
    }
}
