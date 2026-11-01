namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.InscricaoAdministrativaDeTeste;

/// <summary>
/// A consulta administrativa da inscrição omite o que a permissão da classe não alcança e registra só a decisão
/// permitida (ADR-0153): acesso ao resource antes da busca, visibilidade de cada item antes da montagem da resposta.
/// </summary>
public sealed class ObterInscricaoAdministrativaQueryHandlerTests
{
    private static readonly Cpf CpfDeErick = Cpf.Criar(PersonasDeTeste.Candidatas.Single(c => c.Nome.StartsWith("Erick", StringComparison.Ordinal)).Cpf).Value!;

    private static readonly string CodigoAcesso = PermissaoSelecao.InscricoesConsultar.Codigo;
    private static readonly string CodigoPublicos = PermissaoSelecao.DadosPublicosConsultar.Codigo;
    private static readonly string CodigoDocumentosPublicos = PermissaoSelecao.DocumentosPublicosConsultar.Codigo;
    private static readonly string CodigoPessoais = PermissaoSelecao.DadosPessoaisConsultar.Codigo;
    private static readonly string CodigoSensiveis = PermissaoSelecao.DadosSensiveisConsultar.Codigo;
    private static readonly string CodigoRevelar = PermissaoSelecao.DadosIdentificadoresRevelar.Codigo;

    private readonly IDecisoesDaRequisicao _requisicao = Substitute.For<IDecisoesDaRequisicao>();

    private static JsonElement Texto(string valor) => JsonSerializer.SerializeToElement(valor);

    private static InscricaoAdministrativaDeTeste DeFernanda(SituacaoInscricao situacao = SituacaoInscricao.Submetida) =>
        new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond, situacao)
            .Fato("CPF", "IDENTIFICADOR", TipoRenderizacao.Texto, Texto(PersonasDeTeste.FernandaDrumond.Valor), ordem: 0, formato: "CPF")
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto(PersonasDeTeste.NomeFernandaDrumond), ordem: 1)
            .Fato("TIPO_DEFICIENCIA", "SENSIVEL", TipoRenderizacao.Texto, Texto("BAIXA_VISAO"), ordem: 2)
            .Exigencia("LAUDO_MEDICO", ClassificacaoDeProtecao.Sensivel)
            .Exigencia("COMPROVANTE_RESIDENCIA", ClassificacaoDeProtecao.Pessoal);

    [Fact(DisplayName = "Kevin com dados pessoais e sem sensíveis recebe nome e CPF mascarado, sem o tipo de deficiência e sem indício de omissão")]
    public async Task DadoSensivel_OmitidoParaQuemNaoTemAPermissao()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste kevin = new(
            "kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, kevin);

        InscricaoAdministrativaDto dto = resultado.Value!;
        ItemDeRespostaDto[] itens = [.. dto.Secoes.SelectMany(static s => s.Itens)];
        itens.Select(static i => i.FatoCodigo).Should().BeEquivalentTo(["CPF", "NOME"]);
        itens.Single(static i => i.FatoCodigo == "CPF").Valor.GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        JsonSerializer.Serialize(dto).Should().NotContain("BAIXA_VISAO").And.NotContain("TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "Registro só da decisão permitida: Kevin tem o acesso e a classe pessoal registrados, e nenhuma negativa de dado sensível")]
    public async Task Registro_SoDaDecisaoPermitida()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste kevin = new(
            "kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        await Consultar(cenario, kevin);

        await kevin.Verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d =>
                d.Count == 1 && d.OfType<DecisaoPermitida>().Single().Registro.Permissao == CodigoPessoais),
            IntencaoDoRegistro.Projecao,
            Arg.Any<CancellationToken>());
        await kevin.Verificador.DidNotReceive().RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.OfType<DecisaoPermitida>().Any(x => x.Registro.Permissao == CodigoSensiveis)),
            Arg.Any<IntencaoDoRegistro>(),
            Arg.Any<CancellationToken>());
        await kevin.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoAcesso),
            Arg.Is<ResourceContext?>(r => r!.ResourceId == cenario.InscricaoId && r.InscricaoId == cenario.InscricaoId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Diogo com a capacidade de revelar recebe o passaporte de Erick mascarado como identificador e podeRevelar verdadeiro, sem registro de revelação")]
    public async Task Identificador_MascaradoComACapacidadeDeRevelar()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(CpfDeErick)
            .Fato("PASSAPORTE_NUMERO", "IDENTIFICADOR", TipoRenderizacao.Texto, Texto("FX123456"));
        DecisoesDeAcessoDeTeste diogo = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, diogo);

        ItemDeRespostaDto passaporte = resultado.Value!.Secoes.Single().Itens.Single();
        passaporte.Componente.Should().Be(ComponentesDeItem.Identificador);
        passaporte.Valor.GetString().Should().Be(ItensDaInscricao.MascaraTotal);
        passaporte.PodeRevelar.Should().BeTrue();
        JsonSerializer.Serialize(resultado.Value).Should().NotContain("FX123456");
        await diogo.Verificador.DidNotReceive().ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoRevelar), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
        await diogo.Verificador.Received(1).PodeAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoRevelar), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Kevin sem a capacidade de revelar recebe o CPF de Fernanda com o componente cpf, mascarado, e podeRevelar falso")]
    public async Task Cpf_SemACapacidadeDeRevelar()
    {
        DecisoesDeAcessoDeTeste kevin = new(
            "kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(DeFernanda(), kevin);

        ItemDeRespostaDto cpf = resultado.Value!.Secoes.Single().Itens.Single(static i => i.FatoCodigo == "CPF");
        cpf.Componente.Should().Be(ComponentesDeItem.Cpf);
        cpf.Valor.GetString().Should().Be("***.598.363-**");
        cpf.PodeRevelar.Should().BeFalse();
        cpf.Classificacao.Should().Be(ClassificacaoDeProtecao.Pessoal, "o identificador mascarado sai pela permissão de dados pessoais");
    }

    [Fact(DisplayName = "Sem dados pessoais o identificador mascarado não sai: a fonte sem a transformação nunca sai")]
    public async Task Identificador_SemDadosPessoais_NaoSai()
    {
        DecisoesDeAcessoDeTeste leandro = new(
            "leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(DeFernanda(), leandro);

        resultado.Value!.Secoes.Should().BeEmpty();
        JsonSerializer.Serialize(resultado.Value).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain("***.598.363-**");
    }

    [Fact(DisplayName = "Cada arquivo sai pela permissão da classe da exigência: com documentos sensíveis o laudo aparece e o comprovante de residência, não")]
    public async Task Arquivo_PelaPermissaoDaClasse()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste diogo = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, diogo);

        ArquivoAdministrativoDto laudo = resultado.Value!.Arquivos.Should().ContainSingle().Subject;
        laudo.ArquivoId.Should().Be(cenario.ArquivoDe("LAUDO_MEDICO"));
        laudo.NomeDoDocumento.Should().Be("Documento LAUDO_MEDICO");
    }

    [Fact(DisplayName = "Avaliação cega: Leandro vê curso, situação e o artigo publicado, sem nome nem CPF mascarado")]
    public async Task AvaliacaoCega_SemNomeNemCpf()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.KevinCarvalho)
            .Fato("CPF", "IDENTIFICADOR", TipoRenderizacao.Texto, Texto(PersonasDeTeste.KevinCarvalho.Valor), formato: "CPF")
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto(PersonasDeTeste.NomeKevinCarvalho), ordem: 1)
            .Fato("RESUMO_DO_ARTIGO", "PUBLICO", TipoRenderizacao.Texto, Texto("Ensaio sobre o ensino"), ordem: 2)
            .Exigencia("ARTIGO_PUBLICADO", ClassificacaoDeProtecao.Publico)
            .Exigencia("COMPROVANTE_RESIDENCIA", ClassificacaoDeProtecao.Pessoal);
        DecisoesDeAcessoDeTeste leandro = new(
            "leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DocumentosPublicosConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, leandro);

        InscricaoAdministrativaDto dto = resultado.Value!;
        dto.Id.Should().Be(cenario.InscricaoId);
        dto.Situacao.Should().Be(SituacaoInscricao.Submetida);
        dto.OfertaCursoId.Should().Be(cenario.OfertaCursoId);
        dto.NumeroInscricao.Should().BeNull("o número é dado interno");
        dto.Secoes.Single().Itens.Single().FatoCodigo.Should().Be("RESUMO_DO_ARTIGO");
        dto.Arquivos.Single().NomeDoDocumento.Should().Be("Documento ARTIGO_PUBLICADO");
        JsonSerializer.Serialize(dto).Should().NotContain(PersonasDeTeste.NomeKevinCarvalho).And.NotContain("***.");
    }

    [Fact(DisplayName = "Derivado sensível sem base legal congelada não sai, mesmo para quem tem a permissão de dados sensíveis")]
    public async Task Sensivel_SemBaseLegal_NaoSai()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Derivado("COM_BASE", "Com base", "TEXTO", "SENSIVEL", Texto("a"))
            .Derivado("SEM_BASE", "Sem base", "TEXTO", "SENSIVEL", Texto("b"), hipoteseLegal: " ");
        DecisoesDeAcessoDeTeste diogo = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, diogo);

        resultado.Value!.Derivados.Select(static i => i.FatoCodigo).Should().Equal("COM_BASE");
    }

    [Fact(DisplayName = "Classificação não resolvida vale como sensível: só a permissão de dados sensíveis a revela")]
    public async Task ClassificacaoNaoResolvida_ValeSensivel()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("DADO_ANTIGO", "PUBLICO", TipoRenderizacao.Texto, Texto("a"));
        cenario.Exigencia("COMPROVANTE_SEM_EXIGENCIA", ClassificacaoDeProtecao.Pessoal, noEnvelope: false);
        DecisoesDeAcessoDeTeste comPessoais = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosPessoaisConsultar,
            PermissaoSelecao.DocumentosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, comPessoais);

        resultado.Value!.Arquivos.Should().BeEmpty("a exigência que a versão não tem vale como sensível, sem base legal");
    }

    [Fact(DisplayName = "A ajuda do formulário e o nome do catálogo não saem no contrato administrativo: rótulo e descrição vêm do que o processo congelou")]
    public async Task Item_TrazRotuloEDescricaoCongelados_SemAjuda()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto("Fernanda"), ajuda: "Digite o nome como no documento", descricao: "Nome civil do candidato");
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, diogo);

        ItemDeRespostaDto item = resultado.Value!.Secoes.Single().Itens.Single();
        item.Rotulo.Should().Be("Rótulo de NOME");
        item.Descricao.Should().Be("Nome civil do candidato");
        item.Tipo.Should().Be(TiposDeItem.Texto);
        item.Componente.Should().Be(ComponentesDeItem.Padrao);
        JsonSerializer.Serialize(resultado.Value).Should().NotContain("Digite o nome");
    }

    [Fact(DisplayName = "O componente declarado no cadastro e congelado vale sobre o formato e a classificação, em minúsculas")]
    public async Task Componente_DeclaradoValeSobreOResto()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("RENDA_FAMILIAR_PER_CAPITA", "PESSOAL", TipoRenderizacao.Numero, JsonSerializer.SerializeToElement(1234.56m), componente: "MOEDA");
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, diogo);

        ItemDeRespostaDto renda = resultado.Value!.Secoes.Single().Itens.Single();
        renda.Componente.Should().Be("moeda");
        renda.Tipo.Should().Be(TiposDeItem.Numero);
        renda.Valor.GetString().Should().Be("1234.56", "o número sai como texto decimal exato");
    }

    [Fact(DisplayName = "Opção de seleção única sai como código e rótulo")]
    public async Task Opcao_SaiComCodigoERotulo()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("COR_RACA", "PESSOAL", TipoRenderizacao.SelecaoUnica, Texto("PARDA"));
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, diogo);

        ItemDeRespostaDto item = resultado.Value!.Secoes.Single().Itens.Single();
        item.Tipo.Should().Be(TiposDeItem.Opcao);
        item.Valor.GetProperty("codigo").GetString().Should().Be("PARDA");
    }

    [Fact(DisplayName = "Fato derivado sai pelo nome, pela classificação e pelo tipo que a publicação congelou; o derivado sem aprovação fica de fora")]
    public async Task Derivado_SaiPeloQueAPublicacaoCongelou()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Derivado("MODALIDADE", "Modalidade de concorrência", "CATEGORICO", "INTERNO", Texto("AC_PCD"));
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar);

        Result<InscricaoAdministrativaDto> resultado = await Consultar(cenario, diogo);

        ItemDeRespostaDto modalidade = resultado.Value!.Derivados.Should().ContainSingle().Subject;
        modalidade.Rotulo.Should().Be("Modalidade de concorrência");
        modalidade.Tipo.Should().Be(TiposDeItem.Opcao);
    }

    [Fact(DisplayName = "A data do envio sai só com a decisão Interno, e o primeiro envio só é consultado quando ela é permitida")]
    public async Task DataDoEnvio_SaiSoComADecisaoInterna()
    {
        DateTimeOffset primeiro = new(2027, 1, 10, 9, 0, 0, TimeSpan.Zero);
        InscricaoAdministrativaDeTeste cenario = DeFernanda().EnviadaEm(primeiro);
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar);
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        InscricaoAdministrativaDeTeste.Mundo mundoDeDiogo = cenario.Montar();
        InscricaoAdministrativaDeTeste.Mundo mundoDeKevin = cenario.Montar();

        InscricaoAdministrativaDto comInterno = (await Executar(cenario, mundoDeDiogo, diogo, cenario.ProcessoId, cenario.InscricaoId)).Value!;
        InscricaoAdministrativaDto semInterno = (await Executar(cenario, mundoDeKevin, kevin, cenario.ProcessoId, cenario.InscricaoId)).Value!;

        comInterno.SubmetidaEm.Should().Be(primeiro);
        semInterno.SubmetidaEm.Should().BeNull();
        JsonSerializer.Serialize(semInterno).Should().NotContain("submetidaEm");
        await mundoDeDiogo.Inscricoes.Received(1).ObterPrimeirosEnviosAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == cenario.InscricaoId), Arg.Any<CancellationToken>());
        await mundoDeKevin.Inscricoes.DidNotReceiveWithAnyArgs().ObterPrimeirosEnviosAsync(default!, default);
    }

    [Fact(DisplayName = "A seção sai com o título que o formulário do processo congelou; a seção que a versão não tem sai sem título")]
    public async Task Secao_SaiComOTituloCongelado()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto("Fernanda"), secao: "DADOS", ordem: 0)
            .Fato("ANTIGO", "PESSOAL", TipoRenderizacao.Texto, Texto("a"), secao: "SECAO_QUE_A_VERSAO_NAO_TEM", ordem: 1);
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        InscricaoAdministrativaDto dto = (await Consultar(cenario, diogo)).Value!;

        dto.Secoes.Single(static s => s.Codigo == "DADOS").Titulo.Should().Be("Dados");
        dto.Secoes.Single(static s => s.Codigo == "SECAO_QUE_A_VERSAO_NAO_TEM").Titulo.Should().BeNull();
    }

    [Fact(DisplayName = "Leandro com dados públicos e documentos públicos tem as duas decisões registradas, ainda que sejam classes abertas")]
    public async Task Registro_ClassesPublicas_SaoRegistradas()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste leandro = new(
            "leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DocumentosPublicosConsultar);

        await Consultar(cenario, leandro);

        await leandro.Verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d =>
                d.OfType<DecisaoPermitida>().Select(x => x.Registro.Permissao).Order().SequenceEqual(new[] { CodigoDocumentosPublicos, CodigoPublicos }.Order())),
            IntencaoDoRegistro.Projecao,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição de outro processo, inexistente ou em rascunho responde 404 sem decisão gravada nem leitura do conteúdo")]
    public async Task ForaDoAlcance_404SemDecisao()
    {
        InscricaoAdministrativaDeTeste rascunho = DeFernanda(SituacaoInscricao.Rascunho);
        DecisoesDeAcessoDeTeste diogo = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        InscricaoAdministrativaDeTeste.Mundo mundo = rascunho.Montar();

        Result<InscricaoAdministrativaDto> doRascunho = await Executar(rascunho, mundo, diogo, rascunho.ProcessoId, rascunho.InscricaoId);
        Result<InscricaoAdministrativaDto> deOutroProcesso = await Executar(rascunho, mundo, diogo, Guid.CreateVersion7(), rascunho.InscricaoId);
        Result<InscricaoAdministrativaDto> inexistente = await Executar(rascunho, mundo, diogo, rascunho.ProcessoId, Guid.CreateVersion7());

        foreach (Result<InscricaoAdministrativaDto> recusa in new[] { doRascunho, deOutroProcesso, inexistente })
        {
            recusa.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
        }

        await diogo.Verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
        await diogo.Verificador.DidNotReceiveWithAnyArgs().AvaliarAsync(default!, default, default);
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Sem a permissão de acesso à inscrição responde 404 e nada do conteúdo é carregado nem decidido por classe")]
    public async Task SemAcessoAoResource_404SemCarregarOConteudo()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.DadosPessoaisConsultar);
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();

        Result<InscricaoAdministrativaDto> resultado = await Executar(cenario, mundo, kevin, cenario.ProcessoId, cenario.InscricaoId);

        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
        await mundo.Inscricoes.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await kevin.Verificador.DidNotReceiveWithAnyArgs().AvaliarAsync(default!, default, default);
    }

    [Fact(DisplayName = "Falha ao registrar o acesso recusa a consulta: a inscrição não sai")]
    public async Task FalhaAoRegistrar_RecusaAConsulta()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        diogo.Verificador.RegistrarDesfechoAsync(Arg.Any<IReadOnlyCollection<DecisaoDeAcesso>>(), Arg.Any<IntencaoDoRegistro>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker"))));

        Func<Task> consultar = () => Consultar(cenario, diogo);

        await consultar.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    private Task<Result<InscricaoAdministrativaDto>> Consultar(InscricaoAdministrativaDeTeste cenario, DecisoesDeAcessoDeTeste persona) =>
        Executar(cenario, cenario.Montar(), persona, cenario.ProcessoId, cenario.InscricaoId);

    private Task<Result<InscricaoAdministrativaDto>> Executar(
        InscricaoAdministrativaDeTeste cenario, InscricaoAdministrativaDeTeste.Mundo mundo, DecisoesDeAcessoDeTeste persona, Guid processoId, Guid inscricaoId)
    {
        ArgumentNullException.ThrowIfNull(cenario);

        return ObterInscricaoAdministrativaQueryHandler.Handle(
            new ObterInscricaoAdministrativaQuery(processoId, inscricaoId),
            persona.Verificador, new ProjetorDeItens(persona.Verificador, _requisicao), new PortaDePermissaoPorPar<PermissaoSelecao>(),
            mundo.Processos, mundo.Inscricoes, mundo.Documentos, mundo.Codecs, CancellationToken.None);
    }
}
