namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs;

using System.Text.Json;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ExemplosDoContratoDeInscricao
{
    internal const string Rascunho = """
        {
            "ofertaCursoId": "0199e000-0000-7000-8000-000000000001",
            "ofertaCursoSegundaOpcaoId": "0199e000-0000-7000-8000-000000000003",
            "respostas": {
                "NOME_SOCIAL": "Ariel Drumond",
                "RG_NUMERO": "1234567"
            },
            "grupos": {
                "COMPOSICAO_FAMILIAR": [
                    { "id": "membro-1", "respostas": { "PARENTESCO": "MAE" } }
                ]
            },
            "aceites": [
                { "codigo": "TERMO_VERACIDADE", "versaoId": "0199e000-0000-7000-8000-000000000002", "hashVersao": "sha256-abc" }
            ]
        }
        """;

    internal static readonly InscricaoDto Inscricao = new()
    {
        Id = Guid.Parse("0199e000-0000-7000-8000-000000000010"),
        ProcessoSeletivoId = Guid.Parse("0199e000-0000-7000-8000-000000000011"),
        Situacao = CodigoComRotulo<SituacaoInscricao>.De(SituacaoInscricao.Enviada),
        NumeroInscricao = "000123",
        Formulario = new FormularioDaInscricaoDto(
                Guid.Parse("0199e000-0000-7000-8000-000000000012"),
                new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567") },
                new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal),
                [new AceiteDeTermoInput("TERMO_VERACIDADE", Guid.Parse("0199e000-0000-7000-8000-000000000002"), "sha256-abc")]),
        EscolhasDeCurso = new EscolhasDeCursoDaInscricaoDto(
                Guid.Parse("0199e000-0000-7000-8000-000000000001"),
                Guid.Parse("0199e000-0000-7000-8000-000000000003"), 2,
                [new OfertaDeCursoCertameDto(Guid.Parse("0199e000-0000-7000-8000-000000000001"), "Direito", "Bacharelado", "Campus de Marabá", ["MATUTINO"]),
                 new OfertaDeCursoCertameDto(Guid.Parse("0199e000-0000-7000-8000-000000000003"), "Engenharia da Computação", "Bacharelado", "Campus de Marabá", ["VESPERTINO"])]),
        ETag = "\"42\"",
        Avaliacao = new AvaliacaoDaInscricaoDto(new ModalidadesDaInscricaoDto(
                Derivada: true, Situacao: CodigoComRotulo<SituacaoDasModalidades>.De(SituacaoDasModalidades.Concorre), [new ModalidadeDaInscricaoDto("AC", "Ampla concorrência", 0)], []),
                new DocumentosDaInscricaoDto([])),
    };

    internal static readonly ComprovanteDaInscricaoDto Comprovante = new()
    {
        Candidato = new CandidatoDoComprovanteDto(PersonasDeTeste.NomeFernandaDrumond, PersonasDeTeste.NomeSocialFernandaDrumond, UsaNomeSocial: true, PersonasDeTeste.FernandaDrumond.Mascarado),
        Inscricao = new InscricaoNoComprovanteDto
        {
            InscricaoId = Guid.Parse("0199e000-0000-7000-8000-000000000012"),
            Numero = 1,
            Total = 2,
            Vale = false,
            NumeroSeguinte = 2,
            Envio = new EnvioDoComprovanteDto("000123", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), FusoHorarioDto.Institucional, "sha256-def"),
            PrazoDaCorrecao = new PrazoDaCorrecaoDto(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), FusoHorarioDto.Institucional, Aberto: true),
        },
        Processo = new ProcessoNoComprovanteDto
        {
            ProcessoSeletivoId = Guid.Parse("0199e000-0000-7000-8000-000000000011"),
            NomeDoProcesso = "PSS Medicina 2027",
            IdentificadorDoProcesso = "pss-medicina-2027",
            UnidadeAdministradora = "Centro de Processos Seletivos",
        },
        EscolhasDeCurso = new EscolhasDeCursoNoComprovanteDto { NumeroDeOpcoesDeCurso = 1, OpcoesDeCurso = [] },
        Formulario = new FormularioNoComprovanteDto
        {
            Secoes = [],
            Derivados = [],
            TermosAceitos = [],
            Documentos = [new DocumentoDoComprovanteDto("Laudo médico", "Você (candidato)", true, "Satisfeita",
                "Sem o documento Laudo médico, a vantagem que você pleiteia não será concedida.",
                [new ArquivoDoComprovanteDto(new string('a', 64), 2048, "PDF")])],
        },
    };

    internal static readonly MinhaInscricaoDto MinhaInscricao = new()
    {
        InscricaoId = Guid.Parse("0199e000-0000-7000-8000-000000000012"),
        ProcessoSeletivoId = Guid.Parse("0199e000-0000-7000-8000-000000000011"),
        NomeDoProcesso = "PSS Medicina 2027",
        NumeroInscricao = "000123",
        Situacao = CodigoComRotulo<SituacaoInscricao>.De(SituacaoInscricao.Enviada),
        OpcoesDeCurso = [new OpcaoDeCursoDaInscricaoDto(1, "Medicina", "Bacharelado"), new OpcaoDeCursoDaInscricaoDto(2, "Direito", "Bacharelado")],
        CanceladaEm = null,
        MotivoDoCancelamento = null,
        Acoes = new AcoesDaInscricaoDto(PodeContinuar: false, PodeCorrigir: true, PodeCancelar: true, PodeDescartar: false),
        MotivoDasInscricoesFechadas = null,
        NumeroDoUltimoComprovante = 1,
    };
}
