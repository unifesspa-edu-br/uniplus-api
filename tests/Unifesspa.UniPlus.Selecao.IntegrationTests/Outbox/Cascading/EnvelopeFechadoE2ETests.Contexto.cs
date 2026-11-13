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
    private sealed partial record Contexto(CascadingApiFactory Api, HttpClient Client, CatalogosSemeados Catalogos)
    {
        private const string Rota = "/api/selecao/processos-seletivos";

        public Guid ProcessoId { get; init; }

        public Task<HttpResponseMessage> CriarProcessoAsync(string nome) => EnviarAsync(
            HttpMethod.Post, Rota,
            new
            {
                nome,
                tipoProcessoOrigemId = Catalogos.TipoProcessoId,
                origemCandidatos = 1, // OrigemCandidatos.InscricaoPropria
                unidadeAdministradoraOrigemId = Catalogos.UnidadeId,
                localidadeCodigoIbge = "1504208",
                localidadeNome = "Marabá",
                localidadeUf = "PA",
                identificadorLegivel = IdentificadoresDeTeste.NovoValor(),
            },
            ifMatch: null);

        public Task<HttpResponseMessage> PutEtapasAsync(object[] etapas, string? ifMatch) =>
            EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/etapas", etapas, ifMatch);

        public Task<HttpResponseMessage> PutOfertaAtendimentoAsync(
            IReadOnlyList<Guid> condicaoIds, IReadOnlyList<Guid> recursoIds, IReadOnlyList<Guid> tipoDeficienciaIds) =>
            EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/oferta-atendimento",
                new { condicaoIds, recursoIds, tipoDeficienciaIds }, ifMatch: null);

        public Task<HttpResponseMessage> PutDistribuicaoVagasAsync()
        {
            object[] distribuicao =
            [
                new
                {
                    ofertaCursoId = Catalogos.OfertaCursoId,
                    voBase = 60,
                    pr = 0.7500m,
                    regraDistribuicaoCodigo = RegraDistribuicaoVagasCodigo.Lei12711,
                    regraDistribuicaoVersao = "v1",
                    regraAjusteCodigo = RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico,
                    regraAjusteVersao = "v1",
                    referenciaReservaDemograficaId = Catalogos.ReferenciaReservaDemograficaId,
                    modalidadeIds = Catalogos.ModalidadeIdPorCodigo.Values.ToArray(),
                    quadro = Array.Empty<object>(),
                },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/distribuicao-vagas", distribuicao, ifMatch: null);
        }

        public Task<HttpResponseMessage> PutOpcoesDeCursoAsync() => EnviarAsync(
            HttpMethod.Put, $"{Rota}/{ProcessoId}/opcoes-de-curso", new { numero = 1 }, ifMatch: null);

        public Task<HttpResponseMessage> PutBonusRegionalAsync(Guid baseLegalBonusRegionalId) => EnviarAsync(
            HttpMethod.Put, $"{Rota}/{ProcessoId}/bonus-regional",
            new
            {
                aplica = true,
                regraCodigo = RegraBonusCodigo.Multiplicativo,
                regraVersao = "v1",
                fator = 1.2000m,
                teto = 95.5000m,
                fatoElegibilidadeCodigo = FormularioDeTeste.FatoBooleanoDaInscricao,
                baseLegalBonusRegionalId,
            },
            ifMatch: null);

        public Task<HttpResponseMessage> PutTaxaInscricaoAsync() => EnviarAsync(
            HttpMethod.Put, $"{Rota}/{ProcessoId}/taxa-inscricao",
            new
            {
                cobra = false,
                valor = (decimal?)null,
                fundamentos = (string[]?)null,
            },
            ifMatch: null);

        public Task<HttpResponseMessage> PutCriteriosDesempateAsync(Guid objetivaId)
        {
            object[] criterios =
            [
                new { ordem = 1, regraCodigo = CriterioDesempateCodigo.Idoso, regraVersao = "v1", idadeMinima = 60, etapaRef = (Guid?)null, fato = (string?)null, @operador = (string?)null, valor = (string?)null },
                new { ordem = 2, regraCodigo = CriterioDesempateCodigo.MaiorNotaEtapa, regraVersao = "v1", etapaRef = (Guid?)objetivaId, idadeMinima = (int?)null, fato = (string?)null, @operador = (string?)null, valor = (string?)null },
                new { ordem = 3, regraCodigo = CriterioDesempateCodigo.MaiorIdade, regraVersao = "v1", etapaRef = (Guid?)null, idadeMinima = (int?)null, fato = (string?)null, @operador = (string?)null, valor = (string?)null },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/criterios-desempate", criterios, ifMatch: null);
        }

        public Task<HttpResponseMessage> PutOpcoesDeclaradasAsync(string fatoCodigo, object[] opcoes) =>
            EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/fatos/{fatoCodigo}/opcoes", new { opcoes }, ifMatch: null);

        public Task<HttpResponseMessage> PutClassificacaoAsync(Guid objetivaId, string resolucaoPesoAreaEnem)
        {
            object[] regrasEliminacao =
            [
                new { regraCodigo = RegraEliminacaoCodigo.ElimNotaMinimaEtapa, regraVersao = "v1", etapaRef = (Guid?)objetivaId, notaMinima = (decimal?)45.0000m, minimo = (decimal?)null },
                new { regraCodigo = RegraEliminacaoCodigo.ElimCorteEmArea, regraVersao = "v1", etapaRef = (Guid?)null, notaMinima = (decimal?)null, minimo = (decimal?)400.0000m, areaCodigo = "REDACAO" },
                new { regraCodigo = RegraEliminacaoCodigo.ElimZeroEmArea, regraVersao = "v1", etapaRef = (Guid?)null, notaMinima = (decimal?)null, minimo = (decimal?)null },
            ];

            return EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/classificacao",
                new
                {
                    regraCalculoCodigo = RegraCalculoCodigo.FormulaMediaPonderada,
                    regraCalculoVersao = "v1",
                    regraArredondamentoCodigo = RegraArredondamentoCodigo.PrecisaoArredondarCima,
                    regraArredondamentoVersao = "v1",
                    casasArredondamento = 3,
                    regraOrdemAlocacaoCodigo = RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria,
                    regraOrdemAlocacaoVersao = "v1",
                    regrasEliminacao,
                    baseadoEmEnem = true,
                    resolucaoPesoAreaEnem,
                    escalaNotaFinal = 1000m,
                },
                ifMatch: null);
        }
    }
}
