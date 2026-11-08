<?xml version="1.0" encoding="utf-8"?>
<!--
    Turns a library document into an HTML page: a header with the number of books
    and one card per <book>. Works both for the whole file and for a saved fragment.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="utf-8" indent="yes"/>

  <!-- The root template builds the whole page. -->
  <xsl:template match="/library">
    <html lang="uk">
      <head>
        <title>Кафедральна бібліотека</title>
        <style>
          body   { margin: 0; padding: 32px; background: #0B0D12; color: #ECEFF5;
                   font-family: "Segoe UI", Arial, sans-serif; }
          h1     { margin: 0 0 4px; font-size: 28px; }
          .sub   { color: #8B95A8; margin-bottom: 24px; }
          table  { width: 100%; border-collapse: separate; border-spacing: 0 10px; }
          th     { text-align: left; color: #5D667A; font-size: 11px; letter-spacing: 1.5px;
                   text-transform: uppercase; padding: 0 14px; }
          td     { background: #151A24; padding: 14px; vertical-align: top;
                   border-top: 1px solid #273042; border-bottom: 1px solid #273042; }
          td:first-child { border-left: 1px solid #273042; border-radius: 12px 0 0 12px;
                           color: #F5B544; font-weight: bold; }
          td:last-child  { border-right: 1px solid #273042; border-radius: 0 12px 12px 0; }
          .title { font-weight: bold; font-size: 16px; }
          .note  { color: #8B95A8; font-size: 13px; margin-top: 4px; }
          .tag   { display: inline-block; background: #1C2230; border-radius: 6px;
                   padding: 2px 8px; margin: 0 4px 4px 0; font-size: 12px; }
          .none  { color: #5D667A; }
        </style>
      </head>
      <body>
        <h1>Кафедральна бібліотека</h1>
        <div class="sub">Книг: <xsl:value-of select="count(book)"/></div>
        <table>
          <tr>
            <th>№</th><th>Книга</th><th>Автор</th><th>Атрибути</th><th>Читачі</th>
          </tr>
          <xsl:apply-templates select="book"/>
        </table>
      </body>
    </html>
  </xsl:template>

  <!-- One row per book. -->
  <xsl:template match="book">
    <tr>
      <td><xsl:value-of select="position()"/></td>
      <td>
        <div class="title"><xsl:value-of select="title"/></div>
        <div class="note"><xsl:value-of select="annotation"/></div>
      </td>
      <td><xsl:value-of select="author"/></td>
      <td>
        <!-- Every attribute except the id becomes a small tag: books have different attributes. -->
        <xsl:for-each select="@*[name() != 'id']">
          <span class="tag"><xsl:value-of select="name()"/>: <xsl:value-of select="."/></span>
        </xsl:for-each>
      </td>
      <td>
        <xsl:for-each select="readers/reader">
          <xsl:value-of select="@name"/> (<xsl:value-of select="@position"/>)<br/>
        </xsl:for-each>
        <xsl:if test="not(readers/reader)"><span class="none">—</span></xsl:if>
      </td>
    </tr>
  </xsl:template>
</xsl:stylesheet>
