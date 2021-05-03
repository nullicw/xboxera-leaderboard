# Weekly stats

{% assign sorted-weekly-posts = site.tags.weekly | sort: 'post_date' %}

<ul>
  {% for post in sorted-weekly-posts %}
      <li>
        <a href="{{ post.url | relative_url }}">{{ post.title }}</a>
      </li>
  {% endfor %}
</ul>

# Monthly stats

{% assign sorted-monthly-posts = site.tags.monthly | sort: 'post_date' %}

<ul>
  {% for post in sorted-monthly-posts %}
      <li>
        <a href="{{ post.url | relative_url }}">{{ post.title }}</a>
      </li>
  {% endfor %}
</ul>

# Instructions for manual run

1. clone the repository
2. build the executable with VSCode or VS 2019 or download the latest Windows release linked at the top
3. open a Cmdline window inside this folder and run xboxeraleaderboard.exe with the appropriate cmdline options
4. check the output for errors (can happen, open XBL api is not super reliable)
5. copy the two tables in the cmdline output to XboxEra's Discourse editor
6. rejoice!
