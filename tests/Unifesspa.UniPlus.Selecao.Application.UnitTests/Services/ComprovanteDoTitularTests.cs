namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ComprovanteDeTeste;

/// <summary>
/// O cabeçalho, o cartão do candidato e as respostas do comprovante do titular: o cartão não repete as respostas, o que é
/// sensível sai na seção do formulário e nenhum número de documento sai.
/// </summary>
public sealed class ComprovanteDoTitularTests
{
    private static string RotuloDoBasico(string fatoCodigo) =>
        FormularioDeTeste.DadosBasicos().Single(f => f.FatoCodigo == fatoCodigo).Rotulo;

    private static List<FatoColetado> BasicosE(params FatoColetado[] outros) => [.. FormularioDeTeste.DadosBasicos(), .. outros];

    [Fact(DisplayName = "O cabeçalho copia a posição do comprovante e o envio, e o rótulo do item é o da versão")]
    public void Montar_Cabecalho_CopiaPosicaoEEnvio()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(inscricao, Respostas(("APELIDO", "Nanda")));

        ComprovanteDaInscricaoDto dto = Montar(inscricao, comprovante, Envelope([Texto("APELIDO", "Como prefere ser chamada")]), numero: 1, total: 3);

        ConferirCabecalho(dto, inscricao, comprovante);
        TodosOsItens(dto).Should().ContainSingle(i => i.Rotulo == "Como prefere ser chamada").Which.Valor!.Value.GetString().Should().Be("Nanda");
    }

    [Fact(DisplayName = "O envio se lê no fuso institucional, com o id e o nome do domínio")]
    public void Montar_Envio_TrazOFusoInstitucional()
    {
        Inscricao inscricao = UmaInscricao();

        ComprovanteDaInscricaoDto dto = Montar(inscricao, Emitir(inscricao, Respostas()), Envelope());

        dto.Inscricao.Envio.FusoHorario.Id.Should().Be(FusoInstitucional.ZoneId);
        dto.Inscricao.Envio.FusoHorario.Nome.Should().Be(FusoInstitucional.Nome);
    }

    [Fact(DisplayName = "O último comprovante vale e não aponta seguinte; o do meio aponta os dois vizinhos")]
    public void Montar_Vizinhos()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(inscricao, Respostas());
        EnvelopeReidratado envelope = Envelope();

        ComprovanteDaInscricaoDto ultimo = Montar(inscricao, comprovante, envelope, numero: 3, total: 3);
        ComprovanteDaInscricaoDto meio = Montar(inscricao, comprovante, envelope, numero: 2, total: 3);

        ultimo.Inscricao.Vale.Should().BeTrue();
        ultimo.Inscricao.NumeroAnterior.Should().Be(2);
        ultimo.Inscricao.NumeroSeguinte.Should().BeNull();
        meio.Inscricao.Vale.Should().BeFalse();
        (meio.Inscricao.NumeroAnterior, meio.Inscricao.NumeroSeguinte).Should().Be((1, 3));
    }

    [Fact(DisplayName = "O cartão traz o nome civil, o nome social só de quem usa e o CPF mascarado")]
    public void Montar_Cartao_NomeSocialCondicional()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao inscricao = UmaInscricao(cpf);
        EnvelopeReidratado envelope = Envelope(BasicosE());

        ComprovanteDaInscricaoDto comNome = Montar(inscricao, Emitir(inscricao, Respostas(
            ("NOME", PersonasDeTeste.NomeFernandaDrumond), ("DESEJA_NOME_SOCIAL", true), ("NOME_SOCIAL", PersonasDeTeste.NomeSocialFernandaDrumond))), envelope);
        ComprovanteDaInscricaoDto semNome = Montar(inscricao, Emitir(inscricao, Respostas(
            ("NOME", PersonasDeTeste.NomeFernandaDrumond), ("DESEJA_NOME_SOCIAL", false), ("NOME_SOCIAL", PersonasDeTeste.NomeSocialFernandaDrumond))), envelope);

        comNome.Candidato.Should().Be(new CandidatoDoComprovanteDto(
            PersonasDeTeste.NomeFernandaDrumond, PersonasDeTeste.NomeSocialFernandaDrumond, UsaNomeSocial: true, cpf.Mascarado));
        semNome.Candidato.Should().Be(new CandidatoDoComprovanteDto(
            PersonasDeTeste.NomeFernandaDrumond, NomeSocial: null, UsaNomeSocial: false, cpf.Mascarado));
    }

    [Fact(DisplayName = "Os três fatos do nome ficam só no cartão e não se repetem nas seções")]
    public void Montar_FatosDoNome_NaoSeRepetemNasSecoes()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(inscricao, Respostas(
            ("NOME", PersonasDeTeste.NomeFernandaDrumond), ("DESEJA_NOME_SOCIAL", true), ("NOME_SOCIAL", PersonasDeTeste.NomeSocialFernandaDrumond),
            ("APELIDO", "Nanda")));
        string[] rotulosDoNome = RotulosDoNome();

        ComprovanteDaInscricaoDto dto = Montar(
            inscricao, comprovante, Envelope(BasicosE(Texto("APELIDO", "Apelido", ordem: FormularioDeTeste.PrimeiraOrdemDeInscricao))));

        rotulosDoNome.Should().Contain("Nome completo");
        TodosOsItens(dto).Select(static i => i.Rotulo).Should().Contain("Apelido").And.NotContain(rotulosDoNome);
    }

    [Fact(DisplayName = "As respostas de cifra individual saem cada uma na seção do formulário a que pertence, com o valor")]
    public void Montar_Sensiveis_SaemNaSecaoDoFormulario()
    {
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(
            inscricao,
            Respostas(("APELIDO", "Nanda")),
            sensiveis: Respostas(("IDENTIDADE_GENERO", "CIS"), ("ORIENTACAO_SEXUAL", "HETEROSSEXUAL")));
        EnvelopeReidratado envelope = Envelope(BasicosE(Texto("APELIDO", "Apelido", ordem: FormularioDeTeste.PrimeiraOrdemDeInscricao)));

        ComprovanteDaInscricaoDto dto = Montar(inscricao, comprovante, envelope);

        ConferirItensSensiveis(dto);
    }

    [Fact(DisplayName = "O CPF e o número do documento respondidos não saem em claro em nenhum lugar do comprovante")]
    public void Montar_NaoCarregaCpfNemNumeroDeDocumento()
    {
        const string NumeroDoRg = "12.868.927-4";
        Inscricao inscricao = UmaInscricao();
        ComprovanteInscricao comprovante = Emitir(inscricao, Respostas(
            ("CPF", PersonasDeTeste.FernandaDrumond.Valor), ("RG_NUMERO", NumeroDoRg)));

        ComprovanteDaInscricaoDto dto = Montar(inscricao, comprovante, Envelope(BasicosE()));

        string json = JsonSerializer.Serialize(dto);
        json.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain(NumeroDoRg);
        TodosOsItens(dto).Single(i => i.Rotulo == RotuloDoBasico("RG_NUMERO")).Valor!.Value.GetString().Should().Be("•••••••");
        TodosOsItens(dto).Single(i => i.Rotulo == RotuloDoBasico("CPF")).Valor!.Value.GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
    }

    [Fact(DisplayName = "As ocorrências do grupo se chamam Você (candidato) e Integrante n, na ordem do envio")]
    public void Montar_TitulosDasOcorrencias()
    {
        GrupoColetado composicao = GrupoDeComposicao();
        Inscricao inscricao = UmaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = OcorrenciasDaFamilia();

        ComprovanteDaInscricaoDto dto = Montar(inscricao, Emitir(inscricao, Respostas(), grupos: grupos), Envelope(grupos: [composicao]));

        dto.Formulario.Secoes.SelectMany(static s => s.Grupos).Single().Ocorrencias.Select(static o => o.Titulo)
            .Should().Equal("Integrante 1", "Você (candidato)", "Integrante 2");
    }

    private static void ConferirCabecalho(ComprovanteDaInscricaoDto dto, Inscricao inscricao, ComprovanteInscricao comprovante)
    {
        dto.Inscricao.InscricaoId.Should().Be(inscricao.Id);
        dto.Inscricao.Numero.Should().Be(1);
        dto.Inscricao.Total.Should().Be(3);
        dto.Inscricao.Vale.Should().BeFalse();
        dto.Inscricao.NumeroAnterior.Should().BeNull();
        dto.Inscricao.NumeroSeguinte.Should().Be(2);
        dto.Processo.NomeDoProcesso.Should().Be("PSS Medicina 2027");
        dto.Inscricao.Envio.NumeroInscricao.Should().Be("00000001");
        dto.Inscricao.Envio.EnviadoEm.Should().Be(Instante);
        dto.Inscricao.Envio.HashIntegridade.Should().Be(comprovante.HashIntegridade);
    }


    private static string[] RotulosDoNome() =>
    [
        RotuloDoBasico(ConjuntoBasicoDaInscricao.Nome),
        RotuloDoBasico(ConjuntoBasicoDaInscricao.DesejaNomeSocial),
        RotuloDoBasico(ConjuntoBasicoDaInscricao.NomeSocial),
    ];


    private static void ConferirItensSensiveis(ComprovanteDaInscricaoDto dto)
    {
        SecaoDoComprovanteDto basicos = dto.Formulario.Secoes.Single(static s => s.Codigo == ConjuntoBasicoDaInscricao.CodigoDaSecao);
        basicos.Itens.Select(static i => i.Rotulo).Should().Equal(RotuloDoBasico("IDENTIDADE_GENERO"), RotuloDoBasico("ORIENTACAO_SEXUAL"));
        basicos.Itens[0].Valor!.Value.GetProperty("codigo").GetString().Should().Be("CIS");
        basicos.Itens[1].Valor!.Value.GetProperty("codigo").GetString().Should().Be("HETEROSSEXUAL");
        dto.Formulario.Secoes.Where(static s => s.Codigo != ConjuntoBasicoDaInscricao.CodigoDaSecao).SelectMany(static s => s.Itens)
            .Select(static i => i.Rotulo).Should().Equal("Apelido");
    }


    private static GrupoColetado GrupoDeComposicao()
    {
        FatoColetado parentesco = FatoColetadoDeTeste.Coletando("PARENTESCO", 0)
            .ComRotulo("Parentesco")
            .DeTexto("LIVRE")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();
        return GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 1)
            .ComOcorrencias(1, null)
            .IncluindoOCandidato()
            .ComCampos(parentesco, Texto("NOME_MEMBRO", "Nome do membro", 1))
            .Construir();
    }


    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> OcorrenciasDaFamilia()
    {
        return new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new("m1", Respostas(("PARENTESCO", "MAE"), ("NOME_MEMBRO", PersonasDeTeste.Candidatas[2].Nome))),
                new("m2", Respostas(("PARENTESCO", "PROPRIO_CANDIDATO"), ("NOME_MEMBRO", PersonasDeTeste.NomeFernandaDrumond))),
                new("m3", Respostas(("PARENTESCO", "IRMAO"), ("NOME_MEMBRO", PersonasDeTeste.NomeKevinCarvalho))),
            ],
        };
    }

}
