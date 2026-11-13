namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A publicação das versões do termo e a vigência.</summary>
public sealed class TermoDeResponsabilidadeTests
{
    [Fact(DisplayName = "A primeira publicação, sem vigente esperada, vira a versão 1 e a vigente, com o autor gravado")]
    public void Publicar_Primeira_VersaoUm()
    {
        TermoDeResponsabilidade termo = TermosDeTeste.RegistroSemVersao();

        VersaoDoTermo versao = termo.Publicar(TermosDeTeste.Conteudo(), TermosDeTeste.Autor, null, null, TermosDeTeste.PublicadoEm).Value!;

        versao.Numero.Should().Be(1);
        termo.VersaoVigenteId.Should().Be(versao.Id);
        termo.UltimoNumero.Should().Be(1);
        versao.AutorSub.Should().Be("diogo.souza");
        versao.Resumo.Should().Be(TermosDeTeste.Conteudo().Resumo);
    }

    [Fact(DisplayName = "A segunda publicação com a vigente certa vira a versão 2 e a nova vigente")]
    public void Publicar_Segunda_VersaoDois()
    {
        TermoDeResponsabilidade termo = TermosDeTeste.RegistroSemVersao();
        VersaoDoTermo primeira = termo.Publicar(TermosDeTeste.Conteudo(), TermosDeTeste.Autor, null, null, TermosDeTeste.PublicadoEm).Value!;

        VersaoDoTermo segunda = termo.Publicar(TermosDeTeste.Conteudo("segunda"), TermosDeTeste.Autor, primeira, primeira.Id, TermosDeTeste.PublicadoEm).Value!;

        segunda.Numero.Should().Be(2);
        termo.VersaoVigenteId.Should().Be(segunda.Id);
    }

    [Fact(DisplayName = "A publicação que não viu a vigente atual é recusada com conflito, sem versão nova")]
    public void Publicar_VigenteEsperadaErrada_Recusa()
    {
        TermoDeResponsabilidade termo = TermosDeTeste.RegistroSemVersao();
        VersaoDoTermo primeira = termo.Publicar(TermosDeTeste.Conteudo(), TermosDeTeste.Autor, null, null, TermosDeTeste.PublicadoEm).Value!;

        Result<VersaoDoTermo> resultado = termo.Publicar(TermosDeTeste.Conteudo("segunda"), TermosDeTeste.Autor, primeira, null, TermosDeTeste.PublicadoEm);

        resultado.Error!.Code.Should().Be(TermoErrorCodes.VigenteAlterado);
        termo.UltimoNumero.Should().Be(1);
        termo.VersaoVigenteId.Should().Be(primeira.Id);
    }

    [Fact(DisplayName = "O texto igual ao da vigente não se publica de novo")]
    public void Publicar_TextoIgualAoVigente_Recusa()
    {
        TermoDeResponsabilidade termo = TermosDeTeste.RegistroSemVersao();
        VersaoDoTermo primeira = termo.Publicar(TermosDeTeste.Conteudo(), TermosDeTeste.Autor, null, null, TermosDeTeste.PublicadoEm).Value!;

        Result<VersaoDoTermo> resultado = termo.Publicar(TermosDeTeste.Conteudo(), TermosDeTeste.Autor, primeira, primeira.Id, TermosDeTeste.PublicadoEm);

        resultado.Error!.Code.Should().Be(TermoErrorCodes.TextoIgualAoVigente);
        termo.UltimoNumero.Should().Be(1);
    }

    [Fact(DisplayName = "A versão vigente informada tem de ser a do registro: outra é defeito de quem lê")]
    public void Publicar_VigenteDivergenteDoRegistro_Lanca()
    {
        TermoDeResponsabilidade termo = TermosDeTeste.RegistroSemVersao();

        Action publicar = () => termo.Publicar(TermosDeTeste.Conteudo(), TermosDeTeste.Autor, TermosDeTeste.Publicada(), null, TermosDeTeste.PublicadoEm);

        publicar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A exceção da instalação registra a ausência de termo ou a vigente lida")]
    public void TermoNaInstalacao_RegistraASituacao()
    {
        VersaoDoTermo vigente = TermosDeTeste.Publicada();

        TermoNaInstalacao sem = TermoNaInstalacao.Registrar(Guid.NewGuid(), null, TermosDeTeste.PublicadoEm);
        TermoNaInstalacao com = TermoNaInstalacao.Registrar(Guid.NewGuid(), vigente, TermosDeTeste.PublicadoEm);

        sem.Situacao.Should().Be(Enums.SituacaoDoTermoNaInstalacao.SemTermoPublicado);
        sem.VersaoDoTermoId.Should().BeNull();
        com.Situacao.Should().Be(Enums.SituacaoDoTermoNaInstalacao.TermoVigenteSemAceite);
        com.Numero.Should().Be(1);
    }
}
