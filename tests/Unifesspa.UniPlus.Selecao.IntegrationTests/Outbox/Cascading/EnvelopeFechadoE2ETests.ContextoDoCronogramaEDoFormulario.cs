namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class EnvelopeFechadoE2ETests
{
    private sealed partial record Contexto
    {
        public Task<HttpResponseMessage> PutCronogramaFasesAsync()
        {
            object[] fases =
            [
                new
                {
                    ordem = 1,
                    faseCanonicaId = Catalogos.FaseInscricaoId,
                    inicio = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
                    fim = new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero),
                    produtos = Array.Empty<object>(),
                    faseConcluinteCodigo = (string?)null,
                    emiteParecerIndividual = false,
                    bancasRequeridas = Array.Empty<object>(),
                    regraRecurso = (object?)null,
                    unidadeResponsavelId = Catalogos.UnidadeId,
                },
                new
                {
                    ordem = 2,
                    faseCanonicaId = Catalogos.FaseResultadoPreliminarId,
                    inicio = new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero),
                    fim = new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero),
                    // A fase publica preliminar e definitiva da mesma matéria, com o mesmo
                    // tipo de ato, e por isso conclui a si mesma — não declara fase
                    // concluinte. O tipo é semeado à parte de TiposDeAtoSeeder, que só cobre
                    // os dois atos da sessão editorial.
                    produtos = new object[]
                    {
                        new { atoCodigo = CodigoAtoResultado, papel = PapelProdutoFaseCodigo.Preliminar },
                        new { atoCodigo = CodigoAtoResultado, papel = PapelProdutoFaseCodigo.Definitivo },
                    },
                    faseConcluinteCodigo = (string?)null,
                    emiteParecerIndividual = true,
                    bancasRequeridas = new object[] { new { tipoBancaId = Catalogos.TipoBancaId, categoriasDocumentoIds = Array.Empty<Guid>() } },
                    regraRecurso = (object?)null,
                    unidadeResponsavelId = Catalogos.UnidadeId,
                },
                new
                {
                    // A etapa pontuada exige a fase que agrupa etapas no mesmo cronograma
                    // (ProcessoSeletivo.EtapaSemFaseDeAvaliacao). No catalogo fechado, so a
                    // avaliacao tem esse sinalizador — e ela nao publica ato.
                    ordem = 3,
                    faseCanonicaId = Catalogos.FaseAvaliacaoId,
                    inicio = new DateTimeOffset(2026, 3, 22, 8, 0, 0, TimeSpan.Zero),
                    fim = new DateTimeOffset(2026, 3, 22, 18, 0, 0, TimeSpan.Zero),
                    produtos = Array.Empty<object>(),
                    faseConcluinteCodigo = (string?)null,
                    emiteParecerIndividual = false,
                    bancasRequeridas = Array.Empty<object>(),
                    regraRecurso = (object?)null,
                    unidadeResponsavelId = Catalogos.UnidadeId,
                },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/cronograma-fases", fases, ifMatch: null);
        }

        public Task<HttpResponseMessage> PutFatosColetadosAsync()
        {
            // COR_RACA vem da seção do conjunto básico, que a gravação inclui. As perguntas da reserva
            // de vagas são as que as regras das modalidades ofertadas citam.
            string[] perguntasDaReserva =
                ["CONCORRER_EP", "PCD", "CONCORRER_PCD", "INDIGENA", "QUILOMBOLA", "CONCORRER_PPI", "CONCORRER_Q", "CONCORRER_RENDA"];
            object[] fatos =
            [
                new
                {
                    fatoCodigo = "BAIXA_RENDA",
                    ordem = 0,
                    rotulo = "Baixa renda",
                    tipoRenderizacao = TipoRenderizacaoCodigo.Booleano,
                    obrigatoriedade = "SEMPRE",
                    precondicao = new[] { new[] { new { fato = "COR_RACA", @operador = OperadorCodigo.Igual, valor = "PRETA" } } },
                },
                // A classificação é baseada em ENEM: a inscrição pede a edição, entre as declaradas.
                new
                {
                    fatoCodigo = "EDICAO_ENEM",
                    ordem = 1,
                    rotulo = "Qual edição do ENEM você vai usar?",
                    tipoRenderizacao = TipoRenderizacaoCodigo.SelecaoUnica,
                    obrigatoriedade = "SEMPRE",
                },
                .. perguntasDaReserva.Select(static (fato, indice) => (object)new
                {
                    fatoCodigo = fato,
                    ordem = indice + 2,
                    rotulo = fato,
                    tipoRenderizacao = TipoRenderizacaoCodigo.Booleano,
                    obrigatoriedade = "SEMPRE",
                }),
            ];

            return EnviarAsync(
                HttpMethod.Put, FormularioDeInscricaoHttp.RotaDosItens(ProcessoId).ToString(),
                FormularioDeInscricaoHttp.CorpoDosItens(fatos), ifMatch: null);
        }

        public Task<HttpResponseMessage> PutRegrasDerivacaoAsync()
        {
            object[] configuracoes =
            [
                new
                {
                    codigoFato = "EGRESSO_ESCOLA_PUBLICA",
                    regras = new object[]
                    {
                        new
                        {
                            ordem = 0,
                            contribui = (string?)null,
                            quando = new[] { new[] { new { fato = "COR_RACA", @operador = OperadorCodigo.Igual, valor = "PRETA" } } },
                        },
                    },
                },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/regras-derivacao", configuracoes, ifMatch: null);
        }

        /// <summary>
        /// Matriz 8×7, fallback AC — a MESMA forma do <c>esquema_args</c> congelado da regra
        /// <c>REMANEJ-CASCATA-LEI-12711</c> v1 (<c>RegraCatalogoSeed.cs:169-183</c>): para cada
        /// origem federal, os destinos são as outras 7, na ordem declarada em
        /// <see cref="ModalidadesFederaisLei12711.Codigos"/> — sem permutar, porque o handler
        /// compara célula a célula contra o catálogo, não só a forma.
        /// </summary>
        public Task<HttpResponseMessage> PutCascataRemanejamentoAsync()
        {
            IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
            List<object> destinos = [];
            foreach (string origem in origens)
            {
                string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
                for (int i = 0; i < destinosDaOrigem.Length; i++)
                {
                    destinos.Add(new { modalidadeOrigemCodigo = origem, ordem = i + 1, modalidadeDestinoCodigo = destinosDaOrigem[i] });
                }
            }

            return EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/cascata-remanejamento",
                new
                {
                    regraCodigo = RegraRemanejamentoCodigo.Cascata,
                    regraVersao = "v1",
                    fallbackCodigo = ModalidadesFederaisLei12711.Ac,
                    destinos,
                },
                ifMatch: null);
        }

        public Task<HttpResponseMessage> PutDocumentosExigidosAsync(object[] raizes) =>
            EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/documentos-exigidos", raizes, ifMatch: null);

        public Task<HttpResponseMessage> PutFormularioAsync(string titulo, Guid faseId, string? ifMatch) =>
            EnviarAsync(
                HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/INSCRICAO",
                new
                {
                    faseId,
                    titulo,
                    etapas = new object[]
                    {
                        new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, titulo = "Dados" },
                        new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", titulo = "Revisão e aceite" },
                    },
                },
                ifMatch);

        public Task<HttpResponseMessage> PutDivulgacaoAsync(IReadOnlyList<string>? camposPublicos, string? justificativa, string? ifMatch) =>
            EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/divulgacao",
                new { camposPublicos, justificativa }, ifMatch);
    }
}
