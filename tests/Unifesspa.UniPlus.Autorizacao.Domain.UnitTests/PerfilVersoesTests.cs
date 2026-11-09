namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PerfilVersoesTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Publicar = "selecao:processos:publicar";
    private const string Dados = "selecao:inscricoes:ler-dados-pessoais";
    private const string Unidade = "unidade:inscricoes:operar";
    private const string Descricao = "Descrição válida.";

    private static readonly CatalogoDeTeste Catalogo = new();
    private static readonly ContextoDaVersao ComConcessao = new(NomeEmUsoPorOutroPerfil: false, TemConcessaoNaoRevogada: true);
    private static readonly ContextoDaVersao NomeEmUso = new(NomeEmUsoPorOutroPerfil: true, TemConcessaoNaoRevogada: false);

    [Fact(DisplayName = "Versão em vigor troca a vigência: a anterior se encerra, a nova abre e o nome acompanha")]
    public void EntrarEmVigor_SegundaVersao_TrocaAVigencia()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        VersaoDoPerfil primeira = perfil.VersaoVigente!;
        Criar(perfil, "Analista de processos", [Consultar, Publicar]);

        PerfisDeTeste.EntrarEmVigor(perfil, Catalogo).Desfecho.Should().Be(DesfechoDoDocumentoVerificado.EntrouEmVigor);

        primeira.Situacao.Should().Be(SituacaoDaVersao.Encerrada);
        perfil.VersaoVigente!.Numero.Should().Be(2);
        perfil.Nome.Should().Be("Analista de processos");
        perfil.ComposicaoEmVigor.Should().Equal(Consultar, Publicar);
        perfil.Vigencias.Should().HaveCount(2);
        perfil.Vigencias.Count(v => v.Aberta).Should().Be(1);
        perfil.Vigencias.Single(v => v.VersaoId == primeira.Id).Ate.Should().Be(PerfisDeTeste.Instante);
        perfil.VersaoPendente.Should().BeNull();
    }

    [Fact(DisplayName = "Enquanto a nova versão aguarda o documento, a vigente segue decidindo o acesso")]
    public void VersaoPendente_NaoAlteraAComposicaoEmVigor()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);

        Criar(perfil, "Analista", [Consultar, Publicar]);

        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
        perfil.VersaoPendente!.Numero.Should().Be(2);
    }

    [Fact(DisplayName = "Só uma versão pendente por perfil")]
    public void CriarVersao_ComPendente_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(TentarCriar(perfil, "Analista", [Consultar]))
            .Should().Equal(PerfilErrorCodes.VersaoPendenteExistente);
    }

    [Fact(DisplayName = "Perfil desativado não aceita versão nova")]
    public void CriarVersao_PerfilInativo_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        perfil.Desativar();

        CodigosDaRecusa(TentarCriar(perfil, "Analista", [Consultar])).Should().Equal(PerfilErrorCodes.Inativo);
    }

    [Fact(DisplayName = "Nome em uso por outro perfil é recusado")]
    public void CriarVersao_NomeEmUso_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(TentarCriar(perfil, "Outro nome", [Consultar], NomeEmUso))
            .Should().Equal(PerfilErrorCodes.NomeDuplicado);
    }

    [Fact(DisplayName = "Versão que muda o alcance do perfil vigente é recusada")]
    public void CriarVersao_AlcanceDiferenteDaVigente_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(TentarCriar(perfil, "Analista", [Unidade])).Should().Equal(PerfilErrorCodes.AlcanceAlterado);
    }

    [Fact(DisplayName = "Acrescentar permissão pessoal a perfil com concessão exige a proteção de dados")]
    public void CriarVersao_AmpliaComConcessao_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(TentarCriar(perfil, "Analista", [Consultar, Dados], ComConcessao))
            .Should().Equal(PerfilErrorCodes.AmpliacaoExigeProtecaoDeDados);
    }

    [Fact(DisplayName = "Acrescentar permissão pessoal sem concessão não exige a proteção de dados")]
    public void CriarVersao_AmpliaSemConcessao_Aceita() =>
        TentarCriar(PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo), "Analista", [Consultar, Dados])
            .IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Trocar e retirar permissão com concessão é admitido quando nada pessoal se acrescenta")]
    public void CriarVersao_ReduzOuTrocaInternaComConcessao_Aceita()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar, Dados], Catalogo);

        TentarCriar(perfil, "Analista", [Consultar, Publicar], ComConcessao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem versão vigente a versão nova é uma nova primeira versão: o alcance e o nome podem mudar")]
    public void CriarVersao_AposCancelarAPrimeira_NaoCompara()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        perfil.CancelarVersao(perfil.VersaoPendente!.Id, PerfisDeTeste.Autor, PerfisDeTeste.Instante).IsSuccess.Should().BeTrue();

        PerfisDeTeste.Garantir(TentarCriar(perfil, "Operador de unidade", [Unidade], ComConcessao));

        perfil.VersaoPendente!.Numero.Should().Be(2);
        perfil.Nome.Should().Be("Operador de unidade");
        PerfisDeTeste.EntrarEmVigor(perfil, Catalogo).Desfecho.Should().Be(DesfechoDoDocumentoVerificado.EntrouEmVigor);
        perfil.Alcance(Catalogo).Should().Be(TipoDeEscopo.Unidade);
    }

    [Fact(DisplayName = "Cancelar a pendente grava quem cancelou e mantém a vigente")]
    public void CancelarVersao_Pendente_GravaAutoriaEMantemAVigente()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        VersaoDoPerfil pendente = Criar(perfil, "Analista", [Consultar, Publicar]);

        perfil.CancelarVersao(pendente.Id, PerfisDeTeste.Autor, PerfisDeTeste.Instante).IsSuccess.Should().BeTrue();

        pendente.Situacao.Should().Be(SituacaoDaVersao.Cancelada);
        pendente.CanceladaPorSub.Should().Be(PerfisDeTeste.Autor.Sub);
        pendente.CanceladaEm.Should().Be(PerfisDeTeste.Instante);
        perfil.VersaoVigente!.Numero.Should().Be(1);
    }

    [Fact(DisplayName = "Só a versão pendente se cancela")]
    public void CancelarVersao_Vigente_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(perfil.CancelarVersao(perfil.VersaoVigente!.Id, PerfisDeTeste.Autor, PerfisDeTeste.Instante))
            .Should().Equal(PerfilErrorCodes.VersaoNaoPendente);
    }

    [Fact(DisplayName = "Versão de outro perfil ou inexistente não é encontrada")]
    public void CancelarVersao_Inexistente_NaoEncontrada()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(perfil.CancelarVersao(Guid.NewGuid(), PerfisDeTeste.Autor, PerfisDeTeste.Instante))
            .Should().Equal(PerfilErrorCodes.VersaoNaoEncontrada);
    }

    [Fact(DisplayName = "Veredicto verificado de versão cancelada só grava o documento")]
    public void RegistrarVerificado_VersaoCancelada_SoGravaODocumento()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(perfil.ConfirmarDocumento(versao.Id, "etag", PerfisDeTeste.Instante));
        perfil.CancelarVersao(versao.Id, PerfisDeTeste.Autor, PerfisDeTeste.Instante);

        DesfechoDoDocumentoVerificado desfecho = Verificar(perfil, documento).Desfecho;

        desfecho.Should().Be(DesfechoDoDocumentoVerificado.Gravado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        versao.Situacao.Should().Be(SituacaoDaVersao.Cancelada);
        perfil.Vigencias.Should().BeEmpty();
        perfil.TemComposicaoEmVigor.Should().BeFalse();
    }

    [Fact(DisplayName = "Veredicto repetido ou sem documento em verificação é ignorado")]
    public void RegistrarVerificado_Repetido_Ignora()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(
            perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, "etag", PerfisDeTeste.Instante));

        Verificar(perfil, documento).Desfecho.Should().Be(DesfechoDoDocumentoVerificado.EntrouEmVigor);
        Verificar(perfil, documento).Desfecho.Should().Be(DesfechoDoDocumentoVerificado.Ignorado);
        perfil.Vigencias.Should().ContainSingle();
        perfil.RegistrarDocumentoVerificado(Guid.NewGuid(), "a", PerfisDeTeste.Instante, PerfisDeTeste.SemContexto, Catalogo)
            .Desfecho.Should().Be(DesfechoDoDocumentoVerificado.Ignorado);
    }

    [Fact(DisplayName = "Concessão que surge enquanto o documento esperava recusa a versão que amplia")]
    public void RegistrarVerificado_ConcessaoNova_RecusaAAmpliacao()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        Criar(perfil, "Analista", [Consultar, Dados]);
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(
            perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, "etag", PerfisDeTeste.Instante));

        Verificar(perfil, documento, ComConcessao).Desfecho.Should().Be(DesfechoDoDocumentoVerificado.VersaoRecusada);

        perfil.UltimaVersaoRecusada!.MotivoDaRecusa.Should().Be(MotivoDaRecusaDaVersao.AmpliacaoExigeProtecaoDeDados);
        perfil.UltimaVersaoRecusada.Documento!.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        perfil.VersaoVigente!.Numero.Should().Be(1);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
    }

    [Fact(DisplayName = "Nome que outro perfil tomou enquanto o documento esperava recusa a versão")]
    public void RegistrarVerificado_NomeTomado_RecusaAVersao()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(
            perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, "etag", PerfisDeTeste.Instante));

        Verificar(perfil, documento, NomeEmUso).Desfecho.Should().Be(DesfechoDoDocumentoVerificado.VersaoRecusada);

        perfil.UltimaVersaoRecusada!.MotivoDaRecusa.Should().Be(MotivoDaRecusaDaVersao.NomeIndisponivel);
        perfil.TemComposicaoEmVigor.Should().BeFalse();
        perfil.VersaoPendente.Should().BeNull();
    }

    [Fact(DisplayName = "Recusa do verificador libera outro documento para a mesma versão")]
    public void DocumentoRecusado_PermiteNovoDocumento()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao primeiro = PerfisDeTeste.Garantir(perfil.ConfirmarDocumento(versao.Id, "etag", PerfisDeTeste.Instante));

        perfil.RegistrarDocumentoRecusado(primeiro.Id, PerfisDeTeste.Instante).Should().BeTrue();
        DocumentoDaVersao segundo = PerfisDeTeste.Garantir(perfil.NovoDocumento(versao.Id, 4096));

        segundo.Id.Should().NotBe(primeiro.Id);
        segundo.Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio);
        versao.Documento.Should().BeSameAs(segundo);
        versao.Documentos.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Novo documento só vale depois da recusa do anterior")]
    public void NovoDocumento_SemRecusa_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(perfil.NovoDocumento(perfil.VersaoPendente!.Id, 10))
            .Should().Equal(PerfilErrorCodes.DocumentoNaoRecusado);
    }

    [Fact(DisplayName = "Novo documento com tamanho fora do limite é recusado")]
    public void NovoDocumento_TamanhoInvalido_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, "etag", PerfisDeTeste.Instante));
        perfil.RegistrarDocumentoRecusado(documento.Id, PerfisDeTeste.Instante);

        CodigosDaRecusa(perfil.NovoDocumento(perfil.VersaoPendente.Id, 0)).Should().Equal(PerfilErrorCodes.DocumentoInvalido);
    }

    [Fact(DisplayName = "Perfil desativado não emite novo documento nem reemite o envio")]
    public void Documento_PerfilInativo_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Guid versaoId = perfil.VersaoPendente!.Id;
        perfil.Desativar();

        CodigosDaRecusa(perfil.NovoDocumento(versaoId, 10)).Should().Equal(PerfilErrorCodes.Inativo);
        CodigosDaRecusa(perfil.DocumentoParaReemitirEnvio(versaoId)).Should().Equal(PerfilErrorCodes.Inativo);
    }

    [Fact(DisplayName = "O envio só se reemite enquanto o documento aguarda o PUT")]
    public void ReemitirEnvio_SoAguardandoEnvio()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Guid versaoId = perfil.VersaoPendente!.Id;

        PerfisDeTeste.Garantir(perfil.DocumentoParaReemitirEnvio(versaoId)).Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio);
        perfil.ConfirmarDocumento(versaoId, "etag", PerfisDeTeste.Instante);

        CodigosDaRecusa(perfil.DocumentoParaReemitirEnvio(versaoId)).Should().Equal(PerfilErrorCodes.DocumentoNaoAguardaEnvio);
    }

    [Theory(DisplayName = "Confirmação sem ETag utilizável é recusada")]
    [InlineData(null)]
    [InlineData("  ")]
    public void ConfirmarDocumento_SemEtag_Recusa(string? etag)
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, etag, PerfisDeTeste.Instante))
            .Should().Equal(PerfilErrorCodes.DocumentoInvalido);
    }

    [Fact(DisplayName = "Confirmar de novo um documento já em verificação é recusado")]
    public void ConfirmarDocumento_Repetido_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Guid versaoId = perfil.VersaoPendente!.Id;
        perfil.ConfirmarDocumento(versaoId, "etag", PerfisDeTeste.Instante);

        CodigosDaRecusa(perfil.ConfirmarDocumento(versaoId, "etag", PerfisDeTeste.Instante))
            .Should().Equal(PerfilErrorCodes.DocumentoNaoAguardaEnvio);
    }

    [Fact(DisplayName = "Confirmar o documento de versão que deixou de ser pendente é recusado")]
    public void ConfirmarDocumento_VersaoCancelada_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Guid versaoId = perfil.VersaoPendente!.Id;
        perfil.CancelarVersao(versaoId, PerfisDeTeste.Autor, PerfisDeTeste.Instante);

        CodigosDaRecusa(perfil.ConfirmarDocumento(versaoId, "etag", PerfisDeTeste.Instante))
            .Should().Equal(PerfilErrorCodes.VersaoNaoPendente);
    }

    [Fact(DisplayName = "Só o documento verificado se lê")]
    public void DocumentoParaLeitura_SoVerificado()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Guid versaoId = perfil.VersaoPendente!.Id;

        CodigosDaRecusa(perfil.DocumentoParaLeitura(versaoId)).Should().Equal(PerfilErrorCodes.DocumentoNaoVerificado);
        PerfisDeTeste.EntrarEmVigor(perfil, Catalogo);
        PerfisDeTeste.Garantir(perfil.DocumentoParaLeitura(versaoId)).Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        CodigosDaRecusa(perfil.DocumentoParaLeitura(Guid.NewGuid())).Should().Equal(PerfilErrorCodes.VersaoNaoEncontrada);
    }

    [Fact(DisplayName = "As chaves do documento ficam sob o perfil e a versão")]
    public void Documento_ChavesPorPerfilEVersao()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        DocumentoDaVersao documento = perfil.VersaoPendente!.Documento!;
        string prefixo = $"autorizacao/perfil/{perfil.Id:D}/versao/{documento.VersaoId:D}/documento/{documento.Id:D}";

        documento.ChaveDeEnvio.Should().Be($"{prefixo}/envio");
        documento.ChaveDefinitiva.Should().Be($"{prefixo}/definitivo");
    }

    [Fact(DisplayName = "Perfil desativado: a pendente entra em vigor mesmo assim e o perfil segue desativado")]
    public void RegistrarVerificado_PerfilInativo_EntraEmVigor()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(
            perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, "etag", PerfisDeTeste.Instante));
        perfil.Desativar();

        Verificar(perfil, documento).Desfecho.Should().Be(DesfechoDoDocumentoVerificado.EntrouEmVigor);

        perfil.Ativo.Should().BeFalse();
        perfil.TemComposicaoEmVigor.Should().BeTrue();
    }

    [Fact(DisplayName = "Reativar o perfil preserva a versão pendente")]
    public void DesativarEReativar_PreservaPendente()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        Criar(perfil, "Analista", [Consultar, Publicar]);

        perfil.Desativar();
        perfil.Reativar();

        perfil.VersaoPendente.Should().NotBeNull();
        perfil.VersaoVigente.Should().NotBeNull();
    }

    private static VersaoDoPerfil Criar(Perfil perfil, string nome, string[] permissoes) =>
        PerfisDeTeste.Garantir(TentarCriar(perfil, nome, permissoes));

    private static Result<VersaoDoPerfil> TentarCriar(
        Perfil perfil, string nome, string[] permissoes, ContextoDaVersao? contexto = null) =>
        perfil.CriarVersao(
            PerfisDeTeste.Conteudo(nome, Descricao, permissoes, Catalogo),
            contexto ?? PerfisDeTeste.SemContexto,
            PerfisDeTeste.Autor,
            PerfisDeTeste.Instante,
            Catalogo);

    private static ResultadoDoDocumentoVerificado Verificar(Perfil perfil, DocumentoDaVersao documento, ContextoDaVersao? contexto = null) =>
        perfil.RegistrarDocumentoVerificado(
            documento.Id, new string('b', 64), PerfisDeTeste.Instante, contexto ?? PerfisDeTeste.SemContexto, Catalogo);

    private static string[] CodigosDaRecusa(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private static string[] CodigosDaRecusa<T>(Result<T> resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private sealed class CatalogoDeTeste : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("unidade:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) =>
            codigo == Dados ? ClassificacaoDeProtecao.Pessoal : ClassificacaoDeProtecao.Interno;
    }
}
